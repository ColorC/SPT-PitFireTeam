using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using EFT;
using EFT.Builds;
using EFT.UI;
using UnityEngine;

namespace pitTeam.Modules
{
    // Explicit profile allowlist. No Harmony patch, global stamina mutation or other-player writes.
    public static class ScenarioStaminaSupport
    {
        private const float RestoreInterval = 0.05f;
        private static readonly HashSet<string> Profiles = new(StringComparer.Ordinal);
        private static int _ownerThread;
        private static bool _initialized;
        private static float _nextRestore;
        private static string? _activeProfileId;
        private static string? _lastFailure;
        private static Action<string>? _warning;
        private static long _bodyRestores;
        private static long _handRestores;
        private static IntPtr _boundWorld;
        private static IntPtr _boundPlayer;
        private static IntPtr _boundProfile;
        private static bool _boundAllowed;

        // Call once from the existing Unity-owned Awake/Start, after ManagedFramePump.Initialize.
        // Explicitly call again to reload a reviewed manifest. Tick never polls the filesystem.
        public static void Initialize(string? manifestPath = null, Action<string>? warning = null)
        {
            if (_initialized && _ownerThread != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("Scenario stamina must be initialized on its Unity owner thread.");
            _ownerThread = Environment.CurrentManagedThreadId;
            _initialized = true;
            _warning = warning;
            Profiles.Clear();
            _activeProfileId = null;
            _lastFailure = null;
            _nextRestore = 0f;
            ClearBinding();
            string file = manifestPath ?? Path.Combine(BepInEx.Paths.ConfigPath, "pitFireTeam.scenario-stamina.json");
            if (!File.Exists(file)) return;
            try
            {
                if (new FileInfo(file).Length > 65536) throw new InvalidDataException("Manifest is too large.");
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                JsonElement root = document.RootElement;
                if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unknown manifest schema.");
                JsonElement entries = root.GetProperty("profiles");
                if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 64)
                    throw new InvalidDataException("Invalid profile allowlist.");
                HashSet<string> validated = new(StringComparer.Ordinal);
                HashSet<string> seen = new(StringComparer.Ordinal);
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    string? id = entry.GetProperty("profileId").GetString();
                    if (!IsMongoId(id) || !seen.Add(id!)) throw new InvalidDataException("Profile IDs must be unique MongoIDs.");
                    if (entry.GetProperty("infiniteStamina").GetBoolean()) validated.Add(id!);
                }
                Profiles.UnionWith(validated);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is InvalidOperationException || error is InvalidDataException || error is KeyNotFoundException || error is FormatException)
            {
                Profiles.Clear();
                RecordFailure(error);
            }
        }

        // Existing friendlyPlugin.Update is on the Unity owner thread. Call before its dead/menu early returns
        // with the current GameWorld, so account/world disappearance clears the managed active identity.
        public static void Tick(GameWorld? world)
        {
            if (!_initialized || Profiles.Count == 0 || _ownerThread != Environment.CurrentManagedThreadId) return;
            try
            {
                float now = Time.unscaledTime;
                if (now < _nextRestore) return;
                _nextRestore = now + RestoreInterval;
                Player? player = world?.MainPlayer;
                if (world == null || player == null || player.HealthController?.IsAlive != true)
                {
                    ClearBinding();
                    return;
                }
                Profile? profile = player.Profile;
                if (profile == null)
                {
                    ClearBinding();
                    return;
                }
                // ProfileId's IL2CPP string conversion allocates. Read it only when a native world/player/profile
                // identity changes; every tick still verifies all three pointers before touching stamina.
                if (_boundWorld != world.Pointer || _boundPlayer != player.Pointer || _boundProfile != profile.Pointer)
                {
                    string? profileId = player.ProfileId;
                    _boundWorld = world.Pointer;
                    _boundPlayer = player.Pointer;
                    _boundProfile = profile.Pointer;
                    _boundAllowed = profileId != null && Profiles.Contains(profileId);
                    _activeProfileId = _boundAllowed ? profileId : null;
                    _lastFailure = null;
                }
                if (!_boundAllowed) return;
                PhysicalBase? physical = player.Physical;
                if (physical == null) return;
                if (Restore(physical.Stamina)) _bodyRestores++;
                if (Restore(physical.HandsStamina)) _handRestores++;
            }
            catch (Exception error)
            {
                ClearBinding();
                // Native teardown errors stay observable once per error kind/message and cannot break the main update.
                RecordFailure(error);
            }
        }

        private static bool Restore(Stamina? stamina)
        {
            if (stamina == null) return false;
            Compute<float>? capacity = stamina.TotalCapacity;
            if (capacity == null) return false;
            float maximum = capacity.Value;
            if (maximum <= 0f || float.IsNaN(maximum) || float.IsInfinity(maximum)) return false;
            float current = stamina.Current;
            if (float.IsNaN(current) || float.IsInfinity(current) || current >= maximum - 0.001f) return false;
            // Current GameAssembly RVA 0x1ccc4f0 stores the argument directly at Current's field;
            // this API takes an absolute new stamina value, not a restoration delta.
            stamina.UpdateStamina(maximum);
            return true;
        }

        public static object GetSnapshot() => new
        {
            initialized = _initialized,
            allowedProfileCount = Profiles.Count,
            activeProfileId = _activeProfileId,
            bodyRestores = _bodyRestores,
            handRestores = _handRestores,
            lastFailure = _lastFailure,
        };

        private static void ClearBinding()
        {
            _boundWorld = IntPtr.Zero;
            _boundPlayer = IntPtr.Zero;
            _boundProfile = IntPtr.Zero;
            _boundAllowed = false;
            _activeProfileId = null;
        }

        public static bool IsConfiguredProfile(string? profileId) =>
            profileId != null && Profiles.Contains(profileId);

        // Optional menu hook, after the stock EquipmentBuildsScreen.Show has created its tab group.
        // All scenario records stay Custom so the existing teammate loadout dropdown can also use them.
        public static bool SelectScenarioCatalogTab(EquipmentBuildsScreen? screen, string? mainProfileId = null)
        {
            if (!_initialized || Profiles.Count == 0 || _ownerThread != Environment.CurrentManagedThreadId || screen == null) return false;
            mainProfileId ??= screen._session?.Profile?.Id;
            if (!IsConfiguredProfile(mainProfileId) || screen._buildTypesTabGroup == null || screen._customBuildsTab == null) return false;
            if (screen._currentBuildTabType != EEquipmentBuildType.Custom)
                screen._buildTypesTabGroup.Show(screen._customBuildsTab, true);
            return true;
        }

        private static void RecordFailure(Exception error)
        {
            string failure = error.GetType().Name + ": " + error.Message;
            if (string.Equals(_lastFailure, failure, StringComparison.Ordinal)) return;
            _lastFailure = failure;
            _warning?.Invoke("[Scenario] Stamina support failed: " + failure);
        }

        private static bool IsMongoId(string? value)
        {
            if (value == null || value.Length != 24) return false;
            foreach (char ch in value) if (!(ch is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
            return true;
        }
    }
}
