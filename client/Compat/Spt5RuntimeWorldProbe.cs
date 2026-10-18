using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace pitTeam.Compat
{
    internal static class Spt5RuntimeWorldProbe
    {
        private static Type? CoreType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "pitFireTeam")?.GetType(name, false);
        internal static object CaptureScenarioStamina() => CaptureNormalPatchSnapshot("pitTeam.Modules.ScenarioStaminaSupport", "GetSnapshot");
        private static object? CaptureNativeConstructorHooks()
        {
            Assembly? core = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "pitFireTeam");
            if (core == null)
            {
                return null;
            }
            Type helper = core.GetType("pitTeam.Compat.NativeConstructorPatchTargets", false)
                ?? throw new TypeLoadException(
                    "pitFireTeam is loaded but NativeConstructorPatchTargets is missing."
                );
            MethodInfo capture = helper.GetMethod(
                "Capture",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null
            ) ?? throw new MissingMethodException(helper.FullName, "Capture");
            try
            {
                return capture.Invoke(null, null)
                    ?? throw new InvalidOperationException("Native constructor probe returned null.");
            }
            catch (TargetInvocationException error)
            {
                throw new InvalidOperationException(
                    "pitFireTeam native constructor probe failed.",
                    error.InnerException ?? error
                );
            }
        }
        private static object CaptureBossAIDataBinding()
        {
            var helper = CoreType("pitTeam.Patches.RegisteredBossAIDataBinder");
            if (helper == null) return new { supported = false, available = false, error = "RegisteredBossAIDataBinder is unavailable." };
            var capture = helper.GetMethod("GetProbeSnapshot", BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (capture == null) return new { supported = false, available = false, error = "RegisteredBossAIDataBinder.GetProbeSnapshot is unavailable." };
            try
            {
                var snapshot = capture.Invoke(null, null);
                return snapshot == null ? (object)new { supported = false, available = false, error = "Binder snapshot returned null." } : new { supported = true, available = true, snapshot };
            }
            catch (TargetInvocationException error) { return new { supported = true, available = false, error = (error.InnerException ?? error).ToString() }; }
            catch (Exception error) { return new { supported = true, available = false, error = error.ToString() }; }
        }
        internal static object RunBeingHitActionBridgeSmoke()
        {
            Player? mainPlayer = null;
            string referenceMode = "zero-reference";
            try
            {
                mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
                if (mainPlayer != null) referenceMode = "actual-main-player";
                Type type = CoreType("pitTeam.Compat.Spt5BeingHitActionBridge") ?? throw new TypeLoadException("Spt5BeingHitActionBridge unavailable.");
                MethodInfo run = type.GetMethod("RunControlledNativeSmoke", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Player) }, null) ?? throw new MissingMethodException(type.FullName, "RunControlledNativeSmoke(Player)");
                return run.Invoke(null, new object?[] { mainPlayer }) ?? throw new InvalidOperationException("BeingHitAction bridge smoke returned null.");
            }
            catch (TargetInvocationException error) { return new { supported = true, available = false, passed = false, referenceMode, error = (error.InnerException ?? error).ToString() }; }
            catch (Exception error) { return new { supported = false, available = false, passed = false, referenceMode, error = error.ToString() }; }
        }
        private static object CaptureBeingHitActionBridge()
        {
            try
            {
                Type type = CoreType("pitTeam.Compat.Spt5BeingHitActionBridge") ?? throw new TypeLoadException("Spt5BeingHitActionBridge unavailable.");
                MethodInfo capture = type.GetMethod("GetProbeSnapshot", BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null) ?? throw new MissingMethodException(type.FullName, "GetProbeSnapshot");
                return capture.Invoke(null, null) ?? throw new InvalidOperationException("BeingHitAction bridge snapshot returned null.");
            }
            catch (TargetInvocationException error) { return new { supported = true, available = false, error = (error.InnerException ?? error).ToString() }; }
            catch (Exception error) { return new { supported = false, available = false, error = error.ToString() }; }
        }

        private static object CaptureNormalBossLifecycle()
        {
            try
            {
                Type type = CoreType("pitTeam.Patches.BotsControllerPatch") ?? throw new TypeLoadException("BotsControllerPatch unavailable.");
                MethodInfo capture = type.GetMethod("GetNormalBossLifecycleProbeSnapshot", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null) ?? throw new MissingMethodException(type.FullName, "GetNormalBossLifecycleProbeSnapshot");
                return capture.Invoke(null, null) ?? throw new InvalidOperationException("Normal boss lifecycle snapshot returned null.");
            }
            catch (TargetInvocationException error) { return new { supported = true, available = false, error = (error.InnerException ?? error).ToString() }; }
            catch (Exception error) { return new { supported = false, available = false, error = error.ToString() }; }
        }

        private static object CaptureNormalPatchSnapshot(string typeName, string methodName, object[]? arguments = null)
        {
            try
            {
                Type type = CoreType(typeName) ?? throw new TypeLoadException(typeName + " unavailable.");
                Type[] signature = arguments?.Select(value => value.GetType()).ToArray() ?? Type.EmptyTypes;
                MethodInfo capture = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, signature, null)
                    ?? throw new MissingMethodException(type.FullName, methodName);
                return capture.Invoke(null, arguments) ?? throw new InvalidOperationException(methodName + " returned null.");
            }
            catch (TargetInvocationException error) { return new { supported = true, available = false, error = (error.InnerException ?? error).ToString() }; }
            catch (Exception error) { return new { supported = false, available = false, error = error.ToString() }; }
        }

        private static object CaptureNormalSquadPipeline()
        {
            return new { supported = true, available = true,
                botsController = CaptureNormalBotsController(),
                bossPlayers = CaptureNormalBossPlayers(),
                spawnMemberIds = CaptureNormalStaticList("pitTeam.Utils.SpawnHelper", "spawnMemberIds", false),
                rosterGroupPlayers = CaptureNormalStaticList("pitTeam.Patches.MainMenuControllerPatch", "GroupPlayers", true),
                matchmakerGroupPlayers = CaptureNormalMatchmakerGroupPlayers(),
                addActivePLayerPostfix = new { hook = CaptureNormalAddActivePlayerDiagnostics(), registration = CaptureNormalAddActivePlayerRegistration() },
                normalBossLifecycle = CaptureNormalBossLifecycle(),
                spawnAction = CaptureNormalPatchSnapshot("pitTeam.Patches.BotsEventsControllerSpawnPatch", "GetNormalSpawnActionProbeSnapshot"),
                followerPipeline = CaptureNormalPatchSnapshot("pitTeam.Patches.BotsControllerPatch", "GetNormalFollowerPipelineProbeSnapshot"),
                beingHitActionBridge = CaptureBeingHitActionBridge() };
        }

        private static object CaptureNormalAddActivePlayerRegistration()
        {
            try
            {
                Type controllerType = typeof(EFT.BotsController);
                Type playerType = typeof(EFT.Player);
                MethodInfo target = controllerType.GetMethod("AddActivePLayer", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { playerType }, null)
                    ?? throw new MissingMethodException(controllerType.FullName, "AddActivePLayer(Player)");
                Type harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("HarmonyLib.Harmony", false)).FirstOrDefault(type => type != null)
                    ?? throw new TypeLoadException("HarmonyLib.Harmony unavailable in loaded assemblies.");
                MethodInfo getPatchInfo = harmonyType.GetMethod("GetPatchInfo", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(MethodBase) }, null)
                    ?? throw new MissingMethodException(harmonyType.FullName, "GetPatchInfo(MethodBase)");
                object? patchInfo = getPatchInfo.Invoke(null, new object[] { target });
                if (patchInfo == null) return new { supported = true, available = true, registered = false, status = "no-patch-info", target = target.ToString(), metadataToken = target.MetadataToken, prefixes = 0, postfixes = 0, matchingPostfixes = Array.Empty<string>(), error = (string?)null };
                FieldInfo prefixesField = patchInfo.GetType().GetField("Prefixes", BindingFlags.Public | BindingFlags.Instance) ?? throw new MissingFieldException(patchInfo.GetType().FullName, "Prefixes");
                FieldInfo postfixesField = patchInfo.GetType().GetField("Postfixes", BindingFlags.Public | BindingFlags.Instance) ?? throw new MissingFieldException(patchInfo.GetType().FullName, "Postfixes");
                IEnumerable prefixes = prefixesField.GetValue(patchInfo) as IEnumerable ?? throw new InvalidCastException("Harmony Patches.Prefixes is not enumerable.");
                IEnumerable postfixes = postfixesField.GetValue(patchInfo) as IEnumerable ?? throw new InvalidCastException("Harmony Patches.Postfixes is not enumerable.");
                int prefixCount = 0;
                foreach (object? _ in prefixes) prefixCount++;
                int postfixCount = 0;
                var matches = new List<string>();
                foreach (object? patch in postfixes)
                {
                    postfixCount++;
                    if (patch == null) continue;
                    Type patchType = patch.GetType();
                    string? owner = patchType.GetField("owner", BindingFlags.Public | BindingFlags.Instance)?.GetValue(patch) as string;
                    MethodInfo? patchMethod = patchType.GetProperty("PatchMethod", BindingFlags.Public | BindingFlags.Instance)?.GetValue(patch) as MethodInfo;
                    if (owner == "BotsControllerPatch" || patchMethod?.Name == "PatchPostfix")
                        matches.Add((owner ?? "unknown-owner") + ":" + (patchMethod?.DeclaringType?.FullName ?? "unknown-type") + "." + (patchMethod?.Name ?? "unknown-method"));
                }
                bool registered = matches.Count > 0;
                return new { supported = true, available = true, registered, status = registered ? "registered" : "not-registered", target = target.ToString(), metadataToken = target.MetadataToken, prefixes = prefixCount, postfixes = postfixCount, matchingPostfixes = matches, error = (string?)null };
            }
            catch (Exception error) { return new { supported = false, available = false, registered = (bool?)null, status = "error", target = (string?)null, metadataToken = (int?)null, prefixes = (int?)null, postfixes = (int?)null, matchingPostfixes = (object?)null, error = error.ToString() }; }
        }

        private static object CaptureNormalAddActivePlayerDiagnostics()
        {
            try
            {
                Type type = CoreType("pitTeam.Patches.BotsControllerPatch") ?? throw new TypeLoadException("BotsControllerPatch unavailable.");
                int calls = (int)(type.GetField("NormalAddActivePlayerPostfixCalls", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? throw new MissingFieldException(type.FullName, "NormalAddActivePlayerPostfixCalls"));
                int completed = (int)(type.GetField("NormalAddActivePlayerCompletedCalls", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? throw new MissingFieldException(type.FullName, "NormalAddActivePlayerCompletedCalls"));
                int errors = (int)(type.GetField("NormalAddActivePlayerErrors", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? throw new MissingFieldException(type.FullName, "NormalAddActivePlayerErrors"));
                string? stage = type.GetField("NormalAddActivePlayerStage", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                string? firstError = type.GetField("NormalAddActivePlayerFirstError", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                string status = calls == 0 ? "not-observed" : errors > 0 ? "error-observed" : completed == calls ? "completed" : "incomplete-last-call";
                return new { supported = true, available = true, calls, completed, errors, stage, firstError, status, error = (string?)null };
            }
            catch (Exception error) { return new { supported = false, available = false, calls = (int?)null, completed = (int?)null, errors = (int?)null, stage = (string?)null, firstError = (string?)null, status = "error", error = error.ToString() }; }
        }

        private static object CaptureNormalBotsController()
        {
            try
            {
                Type type = CoreType("pitTeam.Patches.BotsControllerPatch") ?? throw new TypeLoadException("BotsControllerPatch unavailable.");
                FieldInfo field = type.GetField("Controller", BindingFlags.Public | BindingFlags.Static) ?? throw new MissingFieldException(type.FullName, "Controller");
                object? value = field.GetValue(null);
                if (value == null) return new { supported = true, available = false, exists = false, pointer = (string?)null, status = "null-controller", error = "Controller is null." };
                Type? owner = value.GetType();
                object? pointer = null;
                while (owner != null && pointer == null) { pointer = owner.GetProperty("Pointer", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetValue(value); owner = owner.BaseType; }
                if (pointer == null) return new { supported = false, available = false, exists = true, pointer = (string?)null, status = "pointer-unavailable", error = "Controller Pointer is unreadable." };
                if (pointer is not IntPtr nativePointer) return new { supported = false, available = false, exists = true, pointer = (string?)null, status = "unsupported-pointer-type", error = "Controller Pointer is not IntPtr." };
                if (nativePointer == IntPtr.Zero) return new { supported = true, available = false, exists = true, pointer = (string?)null, status = "null-native-pointer", error = "Controller Pointer is IntPtr.Zero." };
                return new { supported = true, available = true, exists = true, pointer = nativePointer.ToString("X"), status = "available", error = (string?)null };
            }
            catch (Exception error) { return new { supported = false, available = false, exists = (bool?)null, pointer = (string?)null, status = "error", error = error.ToString() }; }
        }

        private static object CaptureNormalBossPlayers()
        {
            try
            {
                Type type = CoreType("pitTeam.Modules.BossPlayers") ?? throw new TypeLoadException("BossPlayers unavailable.");
                object? instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (instance == null) return new { supported = true, available = false, count = (int?)null, profileIds = (object?)null, status = "null-instance", error = "BossPlayers.Instance is null." };
                MethodInfo getter = type.GetMethod("GetBossPlayers", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) ?? throw new MissingMethodException(type.FullName, "GetBossPlayers");
                object? value = getter.Invoke(instance, null);
                if (value == null) return new { supported = true, available = false, count = (int?)null, profileIds = (object?)null, status = "null-dictionary", error = "GetBossPlayers returned null." };
                if (value is not IDictionary dictionary) return new { supported = false, available = false, count = (int?)null, profileIds = (object?)null, status = "unsupported-dictionary", error = "Boss dictionary is not IDictionary." };
                var ids = new List<string>();
                foreach (DictionaryEntry entry in dictionary) if (entry.Key is string id) ids.Add(id);
                return new { supported = true, available = true, count = dictionary.Count, profileIds = ids, status = "available", error = (string?)null };
            }
            catch (Exception error) { return new { supported = false, available = false, count = (int?)null, profileIds = (object?)null, status = "error", error = error.ToString() }; }
        }

        private static object CaptureNormalStaticList(string typeName, string fieldName, bool players)
        {
            try
            {
                Type type = CoreType(typeName) ?? throw new TypeLoadException(typeName + " unavailable.");
                FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static) ?? throw new MissingFieldException(type.FullName, fieldName);
                return CaptureNormalList(field.GetValue(null), typeName + "." + fieldName, players);
            }
            catch (Exception error) { return new { supported = false, available = false, count = (int?)null, values = (object?)null, status = "error", error = error.ToString() }; }
        }

        private static object CaptureNormalMatchmakerGroupPlayers()
        {
            try
            {
                Assembly? core = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "pitFireTeam");
                FieldInfo? field = core?.GetType("pitTeam.pitFireTeam", false)?.GetField("application", BindingFlags.Public | BindingFlags.Static);
                if (field == null) return new { supported = false, available = false, count = (int?)null, values = (object?)null, status = "application-unavailable", error = "pitFireTeam.application unavailable." };
                TarkovApplication? app = field.GetValue(null) as TarkovApplication;
                if (app == null) return new { supported = true, available = false, count = (int?)null, values = (object?)null, status = "null-application", error = "pitFireTeam.application is null." };
                var controller = app.Matchmaker;
                if (controller == null || controller.GroupPlayers == null || controller.GroupPlayers.List == null)
                    return new { supported = true, available = false, count = (int?)null, values = (object?)null, status = "null-matchmaker-list", error = "Matchmaker, GroupPlayers or native List is null." };
                var nativeList = controller.GroupPlayers.List;
                int count = nativeList.Count;
                var snapshot = new List<object>();
                for (int i = 0; i < count; i++) snapshot.Add(nativeList[i]);
                return CaptureNormalList(snapshot, "TarkovApplication.Matchmaker.GroupPlayers.List[indexer]", true);
            }
            catch (Exception error) { return new { supported = true, available = false, count = (int?)null, values = (object?)null, status = "error", error = error.ToString() }; }
        }

        private static object CaptureNormalList(object? value, string source, bool players)
        {
            if (value == null) return new { supported = true, available = false, count = (int?)null, values = (object?)null, status = "null-list", error = source + " is null." };
            try
            {
                if (value is not IEnumerable items) return new { supported = false, available = false, count = (int?)null, values = (object?)null, status = "unsupported-list", error = source + " is not enumerable." };
                var result = new List<object>();
                foreach (object? item in items)
                {
                    if (item == null) { result.Add(new { available = false, accountId = (string?)null, profileId = (string?)null, error = "Null entry." }); continue; }
                    if (!players)
                    {
                        if (item is string id) result.Add(id);
                        else return new { supported = false, available = false, count = (int?)null, values = (object?)null, status = "unsupported-entry", error = source + " contains non-string." };
                        continue;
                    }
                    string? accountId = ReadNormalString(item, "AccountId");
                    string? profileId = ReadNormalString(item, "ProfileId");
                    if (profileId == null) profileId = ReadNormalString(ReadNormalMember(item, "Info"), "ProfileId");
                    result.Add(new { available = accountId != null || profileId != null, accountId, profileId, error = accountId == null && profileId == null ? "No account/profile ID exposed." : null });
                }
                return new { supported = true, available = true, count = result.Count, values = result, status = "available", error = (string?)null };
            }
            catch (Exception error) { return new { supported = true, available = false, count = (int?)null, values = (object?)null, status = "error", error = error.ToString() }; }
        }

        private static object? ReadNormalMember(object? instance, string name)
        {
            Type? type = instance?.GetType();
            while (type != null)
            {
                PropertyInfo? property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(instance);
                FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(instance);
                type = type.BaseType;
            }
            return null;
        }

        private static string? ReadNormalString(object? instance, string name) => ReadNormalMember(instance, name) as string;

        internal static int Followers()
        {
            var type = CoreType("pitTeam.Modules.BossPlayers");
            var owner = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (owner == null) return 0;
            var field = type!.GetField("_followers", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(type.FullName, "_followers");
            return ((ICollection?)field.GetValue(owner))?.Count ?? 0;
        }
        private static object CaptureHealth(Player player)
        {
            try
            {
                var controller = player.HealthController;
                if (controller == null)
                    return new { supported = true, available = false, isAlive = (bool?)null, bodyParts = (object?)null, error = "HealthController is null." };

                var isAlive = controller.IsAlive;
                var active = player.ActiveHealthController;
                if (active == null)
                    return new { supported = true, available = true, isAlive, bodyParts = new { supported = false, error = "ActiveHealthController is null." } };

                var bodyParts = new List<object>();
                foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
                {
                    var health = active.GetBodyPartHealth(bodyPart, false);
                    bodyParts.Add(new { bodyPart = bodyPart.ToString(), current = health.Current, maximum = health.Maximum, normalized = health.Normalized, atMinimum = health.AtMinimum, atMaximum = health.AtMaximum });
                }
                return new { supported = true, available = true, isAlive, bodyParts = new { supported = true, values = bodyParts } };
            }
            catch (Exception error)
            {
                return new { supported = true, available = false, isAlive = (bool?)null, bodyParts = (object?)null, error = error.ToString() };
            }
        }

        private static object CaptureWeapon(Player player)
        {
            try
            {
                var heldItem = player.HandsController?.Item;
                if (heldItem == null)
                    return new { supported = true, available = false, status = "no-item-in-hands", itemInHandsType = (string?)null, chamberRounds = (int?)null, magazineRounds = (int?)null };

                var weapon = heldItem.TryCast<EFT.InventoryLogic.Weapon>();
                if (weapon == null)
                    return new { supported = true, available = false, status = "item-in-hands-is-not-a-weapon", itemInHandsType = heldItem.GetType().FullName, chamberRounds = (int?)null, magazineRounds = (int?)null };

                return new { supported = true, available = true, status = "weapon-in-hands", itemInHandsType = weapon.GetType().FullName, chamberRounds = (int?)weapon.ChamberAmmoCount, magazineRounds = (int?)weapon.GetCurrentMagazineCount() };
            }
            catch (Exception error)
            {
                return new { supported = true, available = false, status = "error", itemInHandsType = (string?)null, chamberRounds = (int?)null, magazineRounds = (int?)null, error = error.ToString() };
            }
        }

        private static object CaptureFollowerProfiles()
        {
            var type = CoreType("pitTeam.Modules.BossPlayers");
            if (type == null)
                return new { supported = false, error = "pitTeam.Modules.BossPlayers type is unavailable.", count = (int?)null, profileIds = (string[]?)null };

            var owner = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (owner == null)
                return new { supported = true, available = true, count = 0, profileIds = Array.Empty<string>(), errors = Array.Empty<string>() };

            var followersField = type.GetField("_followers", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(type.FullName, "_followers");
            var followerType = CoreType("pitTeam.Components.BotFollowerPlayer")
                ?? throw new TypeLoadException("pitTeam.Components.BotFollowerPlayer is unavailable.");
            var botField = followerType.GetField("_bot", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(followerType.FullName, "_bot");
            var followers = followersField.GetValue(owner) as IEnumerable
                ?? throw new InvalidCastException("BossPlayers._followers is not enumerable.");
            var profileProperty = typeof(BotOwner).GetProperty("ProfileId", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(BotOwner).FullName, "ProfileId");
            var profileIds = new List<string>();
            var errors = new List<string>();
            foreach (var follower in followers)
            {
                if (follower == null) { errors.Add("null follower entry"); continue; }
                var bot = botField.GetValue(follower) as BotOwner;
                if (bot == null) { errors.Add("follower has no BotOwner"); continue; }
                var profileId = profileProperty.GetValue(bot) as string;
                if (string.IsNullOrWhiteSpace(profileId)) { errors.Add("follower BotOwner has no ProfileId"); continue; }
                profileIds.Add(profileId);
            }
            return new { supported = true, available = true, count = profileIds.Count, profileIds, errors };
        }

        private static object CaptureWorldPosition(Func<Vector3> readPosition, out Vector3? position)
        {
            try
            {
                Vector3 value = readPosition();
                position = value;
                return new { supported = true, available = true, status = "available", x = value.x, y = value.y, z = value.z, error = (string?)null };
            }
            catch (Exception error)
            {
                position = null;
                return new { supported = true, available = false, status = "read-error", x = (float?)null, y = (float?)null, z = (float?)null, error = error.ToString() };
            }
        }
        private static object CaptureWorldDistance(Vector3? from, Vector3? to)
        {
            if (!from.HasValue) return new { supported = true, available = false, status = "source-position-unavailable", meters = (float?)null, error = "Source position is unavailable." };
            if (!to.HasValue) return new { supported = true, available = false, status = "reference-position-unavailable", meters = (float?)null, error = "Main-player position is unavailable." };
            try { return new { supported = true, available = true, status = "available", meters = Vector3.Distance(from.Value, to.Value), error = (string?)null }; }
            catch (Exception error) { return new { supported = true, available = false, status = "read-error", meters = (float?)null, error = error.ToString() }; }
        }
        private static string? CaptureNativePointer(object? value)
        {
            try { return value is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase native && native.Pointer != IntPtr.Zero ? native.Pointer.ToString("X") : null; }
            catch { return null; }
        }
        private static object CaptureMainPlayerInteraction(Player? player, Vector3? mainPosition, bool worldAvailable)
        {
            if (player == null || !worldAvailable)
            {
                string error = player == null ? "GamePlayerOwner.MyPlayer is unavailable." : "GameWorld is unavailable.";
                string status = player == null ? "main-player-unavailable" : "game-world-unavailable";
                return new { supported = true, available = false, status, interactableObject = new { supported = true, available = false, error }, interactablePlayer = new { supported = true, available = false, error }, interactionRay = new { supported = true, available = false, error }, handsController = new { supported = true, available = false, canInteract = new { supported = true, available = false, error } }, error };
            }
            EFT.Interactive.InteractableObject? target = null;
            EFT.Player? targetPlayer = null;
            object targetReference = CaptureIl2CppReference(() => target = player.InteractableObject);
            object playerReference = CaptureIl2CppReference(() => targetPlayer = player.InteractablePlayer);
            string? targetPointer = CaptureNativePointer(target);
            string? targetPlayerPointer = CaptureNativePointer(targetPlayer);
            object targetPosition;
            object targetDistance;
            if (target == null)
            {
                targetPosition = new { supported = true, available = false, status = "no-current-interactable-object", error = "Player.InteractableObject is null." };
                targetDistance = new { supported = true, available = false, status = "no-current-interactable-object", meters = (float?)null, error = "Player.InteractableObject is null." };
            }
            else
            {
                object targetPositionValue = CaptureWorldPosition(() => { var transform = target.transform; if (transform == null) throw new InvalidOperationException("InteractableObject.transform is null."); return transform.position; }, out Vector3? targetVector);
                targetPosition = targetPositionValue;
                targetDistance = CaptureWorldDistance(targetVector, mainPosition);
            }
            object targetPlayerPosition;
            object targetPlayerDistance;
            if (targetPlayer == null)
            {
                targetPlayerPosition = new { supported = true, available = false, status = "no-current-interactable-player", error = "Player.InteractablePlayer is null." };
                targetPlayerDistance = new { supported = true, available = false, status = "no-current-interactable-player", meters = (float?)null, error = "Player.InteractablePlayer is null." };
            }
            else
            {
                object targetPlayerPositionValue = CaptureWorldPosition(() => { var transform = targetPlayer.transform; if (transform == null) throw new InvalidOperationException("InteractablePlayer.transform is null."); return transform.position; }, out Vector3? targetPlayerVector);
                targetPlayerPosition = targetPlayerPositionValue;
                targetPlayerDistance = CaptureWorldDistance(targetPlayerVector, mainPosition);
            }
            object rayObservation;
            try
            {
                Ray ray = player.InteractionRay;
                rayObservation = new { supported = true, available = true, status = "available", origin = new { x = ray.origin.x, y = ray.origin.y, z = ray.origin.z }, direction = new { x = ray.direction.x, y = ray.direction.y, z = ray.direction.z }, error = (string?)null };
            }
            catch (Exception error) { rayObservation = new { supported = true, available = false, status = "read-error", origin = (object?)null, direction = (object?)null, error = error.ToString() }; }
            object handsReference = CaptureIl2CppReference(() => player.HandsController);
            object canInteract;
            try
            {
                var hands = player.HandsController;
                canInteract = hands == null ? new { supported = true, available = false, status = "hands-controller-unavailable", value = (bool?)null, error = (string?)"Player.HandsController is null." } : new { supported = true, available = true, status = "available", value = (bool?)hands.CanInteract(), error = (string?)null };
            }
            catch (Exception error) { canInteract = new { supported = true, available = false, status = "read-error", value = (bool?)null, error = error.ToString() }; }
            return new { supported = true, available = target != null, status = target == null ? "no-current-interactable-object" : "available", interactableObject = new { reference = targetReference, pointer = targetPointer, position = targetPosition, distanceFromMainPlayer = targetDistance, error = target == null ? "Player.InteractableObject is null." : null }, interactablePlayer = new { reference = playerReference, pointer = targetPlayerPointer, position = targetPlayerPosition, distanceFromMainPlayer = targetPlayerDistance, error = targetPlayer == null ? "Player.InteractablePlayer is null." : null }, interactionRay = rayObservation, handsController = new { reference = handsReference, canInteract }, error = target == null ? "No current interactable object." : null };
        }
        private static object CaptureCorpseCollider(EFT.Interactive.Corpse corpse, Player? mainPlayer, Vector3? mainPosition)
        {
            UnityEngine.BoxCollider? collider = null;
            string getterError = null;
            try { collider = corpse.BoundCollider; }
            catch (Exception error) { getterError = error.ToString(); }

            if (collider == null)
            {
                object unavailable = new { supported = true, available = false, status = getterError == null ? "collider-null" : "read-error", value = (object?)null, error = getterError ?? "LootItem.BoundCollider returned null." };
                return new
                {
                    supported = true,
                    available = false,
                    status = getterError == null ? "collider-null" : "read-error",
                    pointer = (string?)null,
                    error = getterError,
                    enabled = unavailable,
                    activeInHierarchy = unavailable,
                    center = unavailable,
                    size = unavailable,
                    bounds = unavailable,
                    closestPointToMainPlayer = unavailable,
                    interactionRaycast = unavailable
                };
            }

            string pointer = null;
            string pointerError = null;
            try { pointer = collider.Pointer.ToString("X"); }
            catch (Exception error) { pointerError = error.ToString(); }

            object enabled;
            try { enabled = new { supported = true, available = true, status = "available", value = (bool?)collider.enabled, error = (string?)null }; }
            catch (Exception error) { enabled = new { supported = true, available = false, status = "read-error", value = (bool?)null, error = error.ToString() }; }

            object activeInHierarchy;
            try
            {
                UnityEngine.GameObject gameObject = collider.gameObject;
                activeInHierarchy = gameObject == null
                    ? new { supported = true, available = false, status = "game-object-null", value = (bool?)null, error = (string?)"Collider.gameObject is null." }
                    : new { supported = true, available = true, status = "available", value = (bool?)gameObject.activeInHierarchy, error = (string?)null };
            }
            catch (Exception error) { activeInHierarchy = new { supported = true, available = false, status = "read-error", value = (bool?)null, error = error.ToString() }; }

            object center = CaptureWorldPosition(() => collider.center, out _);
            object size = CaptureWorldPosition(() => collider.size, out _);
            object bounds;
            try
            {
                UnityEngine.Bounds value = collider.bounds;
                bounds = new
                {
                    supported = true,
                    available = true,
                    status = "available",
                    center = new { x = value.center.x, y = value.center.y, z = value.center.z },
                    extents = new { x = value.extents.x, y = value.extents.y, z = value.extents.z },
                    size = new { x = value.size.x, y = value.size.y, z = value.size.z },
                    error = (string?)null
                };
            }
            catch (Exception error) { bounds = new { supported = true, available = false, status = "read-error", center = (object?)null, extents = (object?)null, size = (object?)null, error = error.ToString() }; }

            object closestPoint;
            if (!mainPosition.HasValue)
                closestPoint = new { supported = true, available = false, status = "main-position-unavailable", position = (object?)null, error = "Main-player position is unavailable." };
            else
                closestPoint = CaptureWorldPosition(() => collider.ClosestPoint(mainPosition.Value), out _);

            object raycast;
            if (mainPlayer == null)
                raycast = new { supported = true, available = false, status = "main-player-unavailable", hit = (bool?)null, distance = (object?)null, point = (object?)null, normal = (object?)null, error = "Main player is unavailable." };
            else
            {
                try
                {
                    Ray ray = mainPlayer.InteractionRay;
                    try
                    {
                        bool hit = collider.Raycast(ray, out UnityEngine.RaycastHit hitInfo, float.PositiveInfinity);
                        if (hit)
                        {
                            object hitPoint = CaptureWorldPosition(() => hitInfo.point, out _);
                            object hitNormal = CaptureWorldPosition(() => hitInfo.normal, out _);
                            object hitDistance;
                            try { hitDistance = new { supported = true, available = true, status = "available", value = (float?)hitInfo.distance, error = (string?)null }; }
                            catch (Exception error) { hitDistance = new { supported = true, available = false, status = "read-error", value = (float?)null, error = error.ToString() }; }
                            raycast = new { supported = true, available = true, status = "hit", hit = (bool?)true, distance = hitDistance, point = hitPoint, normal = hitNormal, error = (string?)null };
                        }
                        else raycast = new { supported = true, available = true, status = "miss", hit = (bool?)false, distance = (object?)null, point = (object?)null, normal = (object?)null, error = (string?)null };
                    }
                    catch (Exception error) { raycast = new { supported = true, available = false, status = "raycast-error", hit = (bool?)null, distance = (object?)null, point = (object?)null, normal = (object?)null, error = error.ToString() }; }
                }
                catch (Exception error) { raycast = new { supported = true, available = false, status = "interaction-ray-error", hit = (bool?)null, distance = (object?)null, point = (object?)null, normal = (object?)null, error = error.ToString() }; }
            }

            return new { supported = true, available = true, status = "available", pointer, pointerError, enabled, activeInHierarchy, center, size, bounds, closestPointToMainPlayer = closestPoint, interactionRaycast = raycast, error = (string?)null };
        }

        private static object CaptureNearbyCorpseColliders(EFT.Interactive.Corpse corpse, Vector3? mainPosition, Player? mainPlayer)
        {
            string corpsePointer = null;
            try { corpsePointer = corpse?.Pointer.ToString("X"); }
            catch (Exception error) { return new { supported = true, available = false, status = "corpse-pointer-error", corpsePointer, error = error.ToString(), colliders = new object[0] }; }
            if (corpse == null) return new { supported = true, available = false, status = "corpse-null", corpsePointer, error = (string?)"Corpse is null.", colliders = new object[0] };

            UnityEngine.Component component;
            try { component = corpse.TryCast<UnityEngine.Component>(); }
            catch (Exception error) { return new { supported = true, available = false, status = "corpse-component-cast-error", corpsePointer, error = error.ToString(), colliders = new object[0] }; }
            if (component == null) return new { supported = true, available = false, status = "corpse-not-component", corpsePointer, error = (string?)"Corpse cannot be cast to UnityEngine.Component.", colliders = new object[0] };

            var rootErrors = new List<string>();
            UnityEngine.GameObject rootObject = null;
            bool rootObjectAvailable = false;
            try { rootObject = component.gameObject; rootObjectAvailable = rootObject != null; } catch (Exception error) { rootErrors.Add("gameObject: " + error); }
            string rootName = null;
            int? rootInstanceId = null;
            bool? rootActiveSelf = null;
            bool? rootActiveInHierarchy = null;
            try { if (rootObject != null) rootName = rootObject.name; } catch (Exception error) { rootErrors.Add("name: " + error); }
            try { if (rootObject != null) rootInstanceId = rootObject.GetInstanceID(); } catch (Exception error) { rootErrors.Add("instanceId: " + error); }
            try { if (rootObject != null) rootActiveSelf = rootObject.activeSelf; } catch (Exception error) { rootErrors.Add("activeSelf: " + error); }
            try { if (rootObject != null) rootActiveInHierarchy = rootObject.activeInHierarchy; } catch (Exception error) { rootErrors.Add("activeInHierarchy: " + error); }
            string parentName = null;
            string parentPointer = null;
            string parentError = null;
            string parentStatus = "unknown";
            try
            {
                UnityEngine.Transform transform = component.transform;
                if (transform == null) { parentStatus = "transform-null"; parentError = "Corpse component transform is null."; }
                else
                {
                    UnityEngine.Transform parent = transform.parent;
                    if (parent != null) { parentName = parent.name; parentPointer = parent.Pointer.ToString("X"); parentStatus = "available"; }
                    else parentStatus = "no-parent";
                }
            }
            catch (Exception error) { parentStatus = "read-error"; parentError = error.ToString(); }
            Vector3? corpseRootPosition = null;
            try { UnityEngine.Transform transform = component.transform; if (transform != null) corpseRootPosition = transform.position; }
            catch (Exception error) { rootErrors.Add("transform.position: " + error); }

            bool rayAvailable = false;
            UnityEngine.Ray interactionRay = default;
            string rayError = null;
            if (mainPlayer == null) rayError = "Main player is unavailable.";
            else
            {
                try { interactionRay = mainPlayer.InteractionRay; rayAvailable = true; }
                catch (Exception error) { rayError = error.ToString(); }
            }

            UnityEngine.Collider[] components;
            try { components = component.GetComponentsInChildren<UnityEngine.Collider>(true); }
            catch (Exception error) { return new { supported = true, available = false, status = "get-components-error", corpsePointer, rootObjectAvailable, rootName, rootInstanceId, rootActiveSelf, rootActiveInHierarchy, parentStatus, parentName, parentPointer, parentError, rootErrors, error = error.ToString(), colliders = new object[0] }; }
            if (components == null) return new { supported = true, available = false, status = "components-null", corpsePointer, rootObjectAvailable, rootName, rootInstanceId, rootActiveSelf, rootActiveInHierarchy, parentStatus, parentName, parentPointer, parentError, rootErrors, error = (string?)"GetComponentsInChildren returned null.", colliders = new object[0] };

            const int maximumColliderScanCount = 128;
            const int maximumColliderSamples = 12;
            const float maximumRaycastDistance = 3f;
            int componentCount = components.Length;
            int scanCount = Math.Min(componentCount, maximumColliderScanCount);
            int enabledCount = 0;
            int activeInHierarchyCount = 0;
            int enabledAndActiveCount = 0;
            int raycastHitCount = 0;
            int raycastErrorCount = 0;
            var scanErrors = new List<string>();
            var hitSamples = new List<object>();
            var enabledActiveSamples = new List<object>();
            var stateSamples = new List<object>();
            var inactiveSamples = new List<object>();
            for (int index = 0; index < scanCount; index++)
            {
                UnityEngine.Collider collider;
                try { collider = components[index]; }
                catch (Exception error) { scanErrors.Add("Collider[" + index + "] array read: " + error); continue; }
                if (collider == null) { scanErrors.Add("Collider[" + index + "] is null."); continue; }
                string pointer = null, nativeType = null, name = null;
                string pointerError = null, typeError = null, nameError = null;
                try { pointer = collider.Pointer.ToString("X"); } catch (Exception error) { pointerError = error.ToString(); }
                try { nativeType = collider.GetType().FullName; } catch (Exception error) { typeError = error.ToString(); }
                try { name = collider.name; } catch (Exception error) { nameError = error.ToString(); }
                bool? enabled = null, activeInHierarchy = null;
                string enabledError = null, activeError = null;
                try { enabled = collider.enabled; if (enabled == true) enabledCount++; } catch (Exception error) { enabledError = error.ToString(); }
                UnityEngine.GameObject colliderObject = null;
                try
                {
                    colliderObject = collider.gameObject;
                    if (colliderObject == null) activeError = "Collider.gameObject is null.";
                    else { activeInHierarchy = colliderObject.activeInHierarchy; if (activeInHierarchy == true) activeInHierarchyCount++; }
                }
                catch (Exception error) { activeError = error.ToString(); }
                if (enabled == true && activeInHierarchy == true) enabledAndActiveCount++;
                object bounds = null;
                string boundsError = null;
                float? boundsDistanceFromCorpseRoot = null;
                try
                {
                    UnityEngine.Bounds value = collider.bounds;
                    if (corpseRootPosition.HasValue) boundsDistanceFromCorpseRoot = Vector3.Distance(corpseRootPosition.Value, value.center);
                    bounds = new { supported = true, available = true, center = new { x = value.center.x, y = value.center.y, z = value.center.z }, size = new { x = value.size.x, y = value.size.y, z = value.size.z }, distanceFromCorpseRoot = boundsDistanceFromCorpseRoot, error = (string?)null };
                }
                catch (Exception error) { boundsError = error.ToString(); bounds = new { supported = true, available = false, center = (object?)null, size = (object?)null, distanceFromCorpseRoot = (float?)null, error = boundsError }; }
                bool? rayHit = null;
                float? rayDistance = null;
                object rayPoint = null;
                string colliderRaycastError = null;
                if (rayAvailable)
                {
                    try
                    {
                        bool hit = collider.Raycast(interactionRay, out UnityEngine.RaycastHit hitInfo, maximumRaycastDistance);
                        rayHit = hit;
                        if (hit) { raycastHitCount++; rayDistance = hitInfo.distance; rayPoint = new { x = hitInfo.point.x, y = hitInfo.point.y, z = hitInfo.point.z }; }
                    }
                    catch (Exception error) { raycastErrorCount++; colliderRaycastError = error.ToString(); }
                }
                bool stateUseful = enabled == true || activeInHierarchy == true;
                object sample = new { index, available = pointer != null || nativeType != null || name != null, status = "observed", pointer, pointerError, nativeType, typeError, name, nameError, enabled = new { available = enabled.HasValue, value = enabled, error = enabledError }, activeInHierarchy = new { available = activeInHierarchy.HasValue, value = activeInHierarchy, error = activeError }, bounds, raycast = new { supported = rayAvailable, available = rayAvailable && colliderRaycastError == null, hit = rayHit, maximumDistance = maximumRaycastDistance, distance = rayDistance, point = rayPoint, error = rayAvailable ? colliderRaycastError : rayError } };
                if (rayHit == true) hitSamples.Add(sample);
                else if (enabled == true && activeInHierarchy == true) enabledActiveSamples.Add(sample);
                else if (stateUseful) stateSamples.Add(sample);
                else inactiveSamples.Add(sample);
            }

            var selectedSamples = new List<object>(maximumColliderSamples);
            foreach (var group in new[] { hitSamples, enabledActiveSamples, stateSamples, inactiveSamples })
            {
                foreach (object sample in group)
                {
                    if (selectedSamples.Count >= maximumColliderSamples) break;
                    selectedSamples.Add(sample);
                }
                if (selectedSamples.Count >= maximumColliderSamples) break;
            }
            int unsampledCount = Math.Max(0, componentCount - selectedSamples.Count);
            return new
            {
                supported = true, available = true, status = "available", corpsePointer, rootName, rootInstanceId, rootActiveSelf, rootActiveInHierarchy, parentName, parentPointer, parentError, rootPosition = corpseRootPosition.HasValue ? (object)new { x = corpseRootPosition.Value.x, y = corpseRootPosition.Value.y, z = corpseRootPosition.Value.z } : null, rootErrors,
                componentCount, scannedCount = scanCount, maximumColliderScanCount, scanTruncated = componentCount > scanCount,
                enabledCount, activeInHierarchyCount, enabledAndActiveCount, rayAvailable, rayError, raycastHitCount, raycastErrorCount,
                sampledCount = selectedSamples.Count, maximumColliderSamples, sampleTruncated = unsampledCount > 0, unsampledCount, scanErrors, colliders = selectedSamples
            };
        }
        private static object CaptureCorpses(GameWorld world, Player? mainPlayer, Vector3? mainPosition)
        {


            object observedPlayersCorpses;
 bool observedPlayersAvailable = false;
 int? observedPlayersCount = null;
 string observedPlayersError = null;
 const string observedPlayersScope = "GameWorld.ObservedPlayersCorpses only; does not represent all local loot corpses.";
 try
 {
 var observed = world.ObservedPlayersCorpses;
 if (observed == null)
 {
 observedPlayersError = "GameWorld.ObservedPlayersCorpses is null.";
 observedPlayersCorpses = new { supported = true, available = false, count = (int?)null, error = observedPlayersError };
 }
 else
 {
 observedPlayersAvailable = true;
 observedPlayersCount = (int)observed.Count;
 observedPlayersCorpses = new { supported = true, available = true, count = observedPlayersCount, scope = observedPlayersScope };
 }
 }
 catch (Exception error)
 {
 observedPlayersError = error.ToString();
 observedPlayersCorpses = new { supported = true, available = false, count = (int?)null, error = observedPlayersError };
 }

 try
 {
 var lootItems = world.LootItems;
                if (lootItems == null)
                    return new { supported = true, available = observedPlayersAvailable, count = observedPlayersCount, scope = observedPlayersScope, error = observedPlayersError,
 observedPlayersCorpses, localLootCorpses = new { supported = true, available = false, collectionCount = (int?)null, scannedCount = 0, corpseCount = (int?)null, truncated = false, sampleTruncated = false, samples = new object[0], error = "GameWorld.LootItems is null." } };

                int collectionCount = (int)lootItems.Count;
                const int maximumScanCount = 8192;
                int scanLimit = Math.Min(collectionCount, maximumScanCount);
                int matchedCorpseCount = 0;
                int nearbyColliderCorpseCount = 0;
                bool enumerationComplete = collectionCount <= maximumScanCount;
                var errors = new List<string>();
                var samples = new List<object>();
                int corpseNavMeshPathCount = 0;
                UnityEngine.AI.NavMeshPath corpseNavMeshPath = null;

                for (int index = 0; index < scanLimit; index++)
                {
                    EFT.Interactive.LootItem lootItem;
                    try
                    {
                        lootItem = lootItems.GetByIndex(index);
                    }
                    catch (Exception error)
                    {
                        errors.Add("GetByIndex(" + index + "): " + error);
                        enumerationComplete = false;
                        continue;
                    }
                    if (lootItem == null) continue;

                    EFT.Interactive.Corpse corpse;
                    try
                    {
                        corpse = lootItem.TryCast<EFT.Interactive.Corpse>();
                    }
                    catch (Exception error)
                    {
                        errors.Add("Corpse TryCast at index " + index + ": " + error);
                        enumerationComplete = false;
                        continue;
                    }
                    if (corpse == null) continue;
                    matchedCorpseCount++;

                    string pointer = null;
                    string profileId = null;
                    string itemId = null;
                    string templateId = null;
                    string readError = null;
                    try
                    {
                        pointer = corpse.Pointer.ToString("X");
                        profileId = corpse.PlayerProfileID;
                        var item = corpse.Item;
                        if (item != null)
                        {
                            itemId = item.Id;
                            templateId = item.TemplateId.ToString();
                        }
                    }
                    catch (Exception error)
                    {
                        readError = error.ToString();
                        errors.Add("Corpse read at index " + index + ": " + error);
                        enumerationComplete = false;
                    }

                    string ownerPointer = null;
                    string ownerType = null;
                    string rootPointer = null;
                    string rootType = null;
                    string rootItemId = null;
                    string rootTemplateId = null;
                    string equipmentPointer = null;
                    string dogtagStatus = "not-read";
                    string slotPointer = null;
                    string slotId = null;
                    string containedItemPointer = null;
                    string dogtagItemId = null;
                    string dogtagTemplateId = null;
                    string dogtagProfileId = null;
                    string dogtagSide = null;
                    string dogtagError = null;
                    try
                    {
                        var owner = corpse.ItemOwner;
                        if (owner == null)
                        {
                            dogtagStatus = "owner-null";
                        }
                        else
                        {
                            ownerPointer = owner.Pointer.ToString("X");
                            ownerType = owner.GetType().FullName;
                            var rootItem = owner.RootItem;
                            if (rootItem == null)
                            {
                                dogtagStatus = "root-item-null";
                            }
                            else
                            {
                                rootPointer = rootItem.Pointer.ToString("X");
                                rootType = rootItem.GetType().FullName;
                                rootItemId = rootItem.Id;
                                rootTemplateId = rootItem.TemplateId.ToString();
                                var equipment = rootItem.TryCast<EFT.InventoryLogic.InventoryEquipment>();
                                if (equipment == null)
                                {
                                    dogtagStatus = "root-not-inventory-equipment";
                                }
                                else
                                {
                                    equipmentPointer = equipment.Pointer.ToString("X");
                                    var slot = equipment.GetSlot(EFT.InventoryLogic.EquipmentSlot.Dogtag);
                                    if (slot == null)
                                    {
                                        dogtagStatus = "dogtag-slot-null";
                                    }
                                    else
                                    {
                                        slotPointer = slot.Pointer.ToString("X");
                                        slotId = slot.ID;
                                        var tagItem = slot.ContainedItem;
                                        if (tagItem == null)
                                        {
                                            dogtagStatus = "dogtag-slot-empty";
                                        }
                                        else
                                        {
                                            containedItemPointer = tagItem.Pointer.ToString("X");
                                            dogtagItemId = tagItem.Id;
                                            dogtagTemplateId = tagItem.TemplateId.ToString();
                                            var tag = tagItem.GetItemComponent<EFT.InventoryLogic.DogtagComponent>();
                                            if (tag == null)
                                            {
                                                dogtagStatus = "dogtag-component-null";
                                            }
                                            else
                                            {
                                                dogtagProfileId = tag.ProfileId;
                                                dogtagSide = tag.Side.ToString();
                                                dogtagStatus = "available";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        dogtagStatus = "error";
                        dogtagError = error.ToString();
                        errors.Add("Corpse dogtag read at index " + index + ": " + error);
                        enumerationComplete = false;
                    }

                    object corpsePosition = CaptureWorldPosition(() => { var transform = corpse.transform; if (transform == null) throw new InvalidOperationException("Corpse.transform is null."); return transform.position; }, out Vector3? corpseVector);
                    object corpseDistance = CaptureWorldDistance(corpseVector, mainPosition);
                    object navMeshPath;
                    float? corpseDistanceMeters = corpseVector.HasValue && mainPosition.HasValue ? Vector3.Distance(corpseVector.Value, mainPosition.Value) : (float?)null;
                    object nearbyColliderObservation = new { supported = true, available = false, status = !mainPosition.HasValue ? "main-position-unavailable" : "outside-distance-limit", corpsePointer = (string?)pointer, distanceMeters = corpseDistanceMeters, maximumDistanceMeters = 5f, error = !mainPosition.HasValue ? "Main-player position is unavailable." : (string?)null, colliders = new object[0] };
                    if (samples.Count < 32 && corpseDistanceMeters.HasValue && corpseDistanceMeters.Value <= 5f && nearbyColliderCorpseCount < 2)
                    {
                        nearbyColliderCorpseCount++;
                        nearbyColliderObservation = CaptureNearbyCorpseColliders(corpse, mainPosition, mainPlayer);
                    }
                    else if (corpseDistanceMeters.HasValue && corpseDistanceMeters.Value <= 5f && nearbyColliderCorpseCount >= 2)
                    {
                        nearbyColliderObservation = new { supported = true, available = false, status = "nearby-corpse-limit", corpsePointer = (string?)pointer, distanceMeters = corpseDistanceMeters, maximumDistanceMeters = 5f, maximumCorpses = 2, error = (string?)null, colliders = new object[0] };
                    }
                    bool withinNavMeshRange = corpseDistanceMeters.HasValue && !float.IsNaN(corpseDistanceMeters.Value) && !float.IsInfinity(corpseDistanceMeters.Value) && corpseDistanceMeters.Value <= 80f;
                    bool hasDogtag = dogtagStatus == "available" && !string.IsNullOrWhiteSpace(dogtagItemId);
                    bool qaProfileMatch = false;
                    string qaProfileReadError = null;
                    if (mainPlayer != null && !string.IsNullOrWhiteSpace(profileId))
                    {
                        try { qaProfileMatch = string.Equals(profileId, mainPlayer.ProfileId, StringComparison.Ordinal); }
                        catch (Exception error) { qaProfileReadError = error.ToString(); }
                    }
                    if (!mainPosition.HasValue || !corpseVector.HasValue)
                        navMeshPath = new { supported = true, available = false, status = "not-selected-position-unavailable", reason = "Main-player or corpse position is unavailable.", corners = new object[0], cornerCount = (int?)null, cornersTruncated = false };
                    else if (!withinNavMeshRange)
                        navMeshPath = new { supported = true, available = false, status = "not-selected-distance", distanceMeters = corpseDistanceMeters, maximumDistanceMeters = 80f, corners = new object[0], cornerCount = (int?)null, cornersTruncated = false };
                    else if (!hasDogtag && qaProfileReadError != null)
                        navMeshPath = new { supported = true, available = false, status = "not-selected-profile-check-error", error = qaProfileReadError, corners = new object[0], cornerCount = (int?)null, cornersTruncated = false };
                    else if (!hasDogtag && !qaProfileMatch)
                        navMeshPath = new { supported = true, available = false, status = "not-selected-no-dogtag-or-qa-profile", distanceMeters = corpseDistanceMeters, corners = new object[0], cornerCount = (int?)null, cornersTruncated = false };
                    else if (corpseNavMeshPathCount >= 3)
                        navMeshPath = new { supported = true, available = false, status = "not-selected-world-limit", selectedPathCount = corpseNavMeshPathCount, maximumSelectedPaths = 3, corners = new object[0], cornerCount = (int?)null, cornersTruncated = false };
                    else
                    {
                        corpseNavMeshPathCount++;
                        try
                        {
                            corpseNavMeshPath ??= new UnityEngine.AI.NavMeshPath();
                            NavMeshPathReadback pathReadback = CaptureVectorNavMeshPath(corpseNavMeshPath, mainPosition.Value, corpseVector.Value, 2f, 10f);
                            navMeshPath = new { supported = true, available = pathReadback.Available, status = pathReadback.Status, selected = true, distanceMeters = corpseDistanceMeters, selectedBy = hasDogtag ? "dogtag" : "qa-profile", pathStatus = pathReadback.PathStatus, complete = pathReadback.Complete, startSampled = pathReadback.StartSampled, destinationSampled = pathReadback.DestinationSampled, sampledStart = pathReadback.SampledStart, sampledDestination = pathReadback.SampledDestination, corners = pathReadback.Corners, cornerCount = pathReadback.CornerCount, cornersTruncated = pathReadback.CornersTruncated, error = pathReadback.Error };
                        }
                        catch (Exception error)
                        {
                            navMeshPath = new { supported = true, available = false, status = "read-error", selected = true, distanceMeters = corpseDistanceMeters, error = error.ToString(), corners = new object[0], cornerCount = (int?)null, cornersTruncated = false };
                        }
                    }

                    if (samples.Count < 32)
                        samples.Add(new
                        {
                            index,
                            pointer,
                            profileId,
                            itemId,
                            templateId,
                            position = corpsePosition,
                            distanceFromMainPlayer = corpseDistance,
                            readError,
                            dogtag = new { status = dogtagStatus, ownerPointer, ownerType, rootPointer, rootType, rootItemId, rootTemplateId, equipmentPointer, slotPointer, slotId, containedItemPointer, dogtagItemId, dogtagTemplateId, dogtagProfileId, dogtagSide, error = dogtagError },
                            navMeshPath,
                            collider = CaptureCorpseCollider(corpse, mainPlayer, mainPosition),
                            nearbyColliderObservation
                        });
                }

                bool available = enumerationComplete && errors.Count == 0;
                return new
                {
                    supported = true,
 available = observedPlayersAvailable,
 count = observedPlayersCount,
 scope = observedPlayersScope,
 error = observedPlayersError,
 observedPlayersCorpses,
                    localLootCorpses = new
                    {
                        supported = true,
                        available,
                        collectionCount,
                        scannedCount = scanLimit,
                        corpseCount = available ? (int?)matchedCorpseCount : null,
                        observedMatches = matchedCorpseCount,
                        truncated = collectionCount > maximumScanCount,
                        sampleTruncated = matchedCorpseCount > samples.Count,
                        samples,
                        errors
                    }
                };
            }
            catch (Exception error)
            {
                return new
                {
                    supported = true,
 available = observedPlayersAvailable,
 count = observedPlayersCount,
 scope = observedPlayersScope,
 error = observedPlayersError,
 observedPlayersCorpses,
                    localLootCorpses = new { supported = true, available = false, collectionCount = (int?)null, scannedCount = 0, corpseCount = (int?)null, truncated = false, sampleTruncated = false, samples = new object[0], error = error.ToString() }
                };
            }
        }
        private sealed class NavMeshPathReadback
        {
            public bool Available { get; }
            public string Status { get; }
            public string? PathStatus { get; }
            public bool? Complete { get; }
            public bool? StartSampled { get; }
            public bool? DestinationSampled { get; }
            public object[] Corners { get; }
            public int? CornerCount { get; }
            public bool CornersTruncated { get; }
            public object? SampledStart { get; }
            public object? SampledDestination { get; }
            public string? Error { get; }

            public NavMeshPathReadback(bool available, string status, string? pathStatus, bool? complete, bool? startSampled, bool? destinationSampled, object[] corners, int? cornerCount, bool cornersTruncated, object? sampledStart, object? sampledDestination, string? error)
            {
                Available = available;
                Status = status;
                PathStatus = pathStatus;
                Complete = complete;
                StartSampled = startSampled;
                DestinationSampled = destinationSampled;
                Corners = corners;
                CornerCount = cornerCount;
                CornersTruncated = cornersTruncated;
                SampledStart = sampledStart;
                SampledDestination = sampledDestination;
                Error = error;
            }
        }

        private static NavMeshPathReadback CaptureVectorNavMeshPath(UnityEngine.AI.NavMeshPath path, Vector3 startPosition, Vector3 destinationPosition, float startSampleRadius, float destinationSampleRadius)
        {
            try
            {
                const int areaMask = UnityEngine.AI.NavMesh.AllAreas;
                if (!UnityEngine.AI.NavMesh.SamplePosition(startPosition, out UnityEngine.AI.NavMeshHit startHit, startSampleRadius, areaMask))
                    return new NavMeshPathReadback(false, "start-not-sampled", null, null, false, false, Array.Empty<object>(), null, false, null, null, null);
                if (!UnityEngine.AI.NavMesh.SamplePosition(destinationPosition, out UnityEngine.AI.NavMeshHit destinationHit, destinationSampleRadius, areaMask))
                    return new NavMeshPathReadback(false, "destination-not-sampled", null, null, true, false, Array.Empty<object>(), null, false, new { x = startHit.position.x, y = startHit.position.y, z = startHit.position.z }, null, null);
                path.ClearCorners();
                bool calculated = UnityEngine.AI.NavMesh.CalculatePath(startHit.position, destinationHit.position, areaMask, path);
                string pathStatus = path.status.ToString();
                bool complete = path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                var nativeCorners = path.corners;
                int cornerCount = nativeCorners?.Length ?? 0;
                int sampleCount = Math.Min(cornerCount, 64);
                var corners = new List<object>(sampleCount);
                for (int cornerIndex = 0; cornerIndex < sampleCount; cornerIndex++)
                {
                    Vector3 corner = nativeCorners[cornerIndex];
                    corners.Add(new { x = corner.x, y = corner.y, z = corner.z });
                }
                return new NavMeshPathReadback(calculated, calculated ? "calculated" : "calculate-path-failed", pathStatus, complete, true, true, corners.ToArray(), cornerCount, cornerCount > sampleCount, new { x = startHit.position.x, y = startHit.position.y, z = startHit.position.z }, new { x = destinationHit.position.x, y = destinationHit.position.y, z = destinationHit.position.z }, null);
            }
            catch (Exception error)
            {
                return new NavMeshPathReadback(false, "read-error", null, null, null, null, Array.Empty<object>(), null, false, null, null, error.ToString());
            }
        }

        private static object CaptureExfilPath(UnityEngine.AI.NavMeshPath path, Player? mainPlayer, Vector3 exitPosition)
        {
            if (mainPlayer?.transform == null)
                return new { supported = true, available = false, status = "main-player-transform-unavailable", pathStatus = (string?)null, complete = (bool?)null, startSampled = false, exitSampled = false, corners = new object[0], cornerCount = (int?)null, cornersTruncated = false, error = (string?)null };
            NavMeshPathReadback readback = CaptureVectorNavMeshPath(path, mainPlayer.transform.position, exitPosition, 2f, 10f);
            string status = readback.Status == "start-not-sampled" ? "main-player-not-sampled-to-navmesh" : readback.Status == "destination-not-sampled" ? "exfil-not-sampled-to-navmesh" : readback.Status;
            return new { supported = true, available = readback.Available, status, pathStatus = readback.PathStatus, complete = readback.Complete, startSampled = readback.StartSampled, exitSampled = readback.DestinationSampled, corners = readback.Corners, cornerCount = readback.CornerCount, cornersTruncated = readback.CornersTruncated, error = readback.Error, sampledStart = readback.SampledStart, sampledExit = readback.SampledDestination };
        }
        private static object CaptureExits(GameWorld world, Player? mainPlayer)
        {
            try
            {
                var controller = world.ExfiltrationController;
                if (controller == null)
                    return new { supported = true, available = false, points = (object?)null, error = "GameWorld.ExfiltrationController is null." };
                var points = controller.ExfiltrationPoints;
                if (points == null)
                    return new { supported = true, available = false, points = (object?)null, error = "ExfiltrationController.ExfiltrationPoints is null." };
                var rows = new List<object>();
                var index = 0;
                var eligiblePointIds = new HashSet<string>(StringComparer.Ordinal);
                bool mainEligibilityAvailable = false;
                string mainEligibilityStatus = mainPlayer?.Profile == null ? "main-player-profile-unavailable" : "eligible-points-unavailable";
                string? mainEligibilityError = null;
                if (mainPlayer?.Profile != null)
                {
                    try
                    {
                        var eligiblePoints = controller.EligiblePoints(mainPlayer.Profile);
                        if (eligiblePoints != null)
                        {
                            foreach (var eligiblePoint in eligiblePoints)
                                if (eligiblePoint != null) eligiblePointIds.Add(eligiblePoint.Id.ToString());
                            mainEligibilityAvailable = true;
                            mainEligibilityStatus = "available";
                        }
                        else mainEligibilityStatus = "null-eligible-points";
                    }
                    catch (Exception eligibilityError)
                    {
                        mainEligibilityStatus = "read-error";
                        mainEligibilityError = eligibilityError.ToString();
                    }
                }
                var navMeshPath = new UnityEngine.AI.NavMeshPath();
                foreach (var point in points)
                {
                    if (point == null) { rows.Add(new { index, error = "null exfiltration point" }); index++; continue; }
                    try
                    {
                        var settings = point.Settings;
                        var transform = point.transform;
                        object? position = transform == null ? null : new { x = transform.position.x, y = transform.position.y, z = transform.position.z };
                        rows.Add(new { index, status = point.Status.ToString(), startTime = point.ExfiltrationStartTime, hasRequirements = point.HasRequirements, queuedPlayers = point.QueuedPlayers?.Count ?? 0, id = point.Id.ToString(), name = settings?.Name, entryPoints = settings?.EntryPoints, eligibleForMainPlayer = mainEligibilityAvailable ? eligiblePointIds.Contains(point.Id.ToString()) : (bool?)null, eligibleEntryPoints = point.EligibleEntryPoints?.ToArray(), position, transformStatus = transform == null ? "null-transform" : "available", navMeshPath = transform == null ? (object)new { supported = true, available = false, status = "null-transform", error = (string?)null } : CaptureExfilPath(navMeshPath, mainPlayer, transform.position) });
                    }
                    catch (Exception pointError)
                    {
                        rows.Add(new { index, status = "read-error", error = pointError.ToString() });
                    }
                    index++;
                }
                return new { supported = true, available = true, mainPlayerEntryPoint = mainPlayer?.Profile?.Info?.EntryPoint, mainPlayerEligibility = new { supported = true, available = mainEligibilityAvailable, status = mainEligibilityStatus, eligiblePointCount = mainEligibilityAvailable ? eligiblePointIds.Count : (int?)null, error = mainEligibilityError }, points = rows };
            }
            catch (Exception error)
            {
                return new { supported = true, available = false, points = (object?)null, error = error.ToString() };
            }
        }

        private static object CaptureIl2CppReference(Func<object?> read)
        {
            try
            {
                object? value = read();
                string? pointer = null, nativeClassName = null, nativeClassNamespace = null, nativeClassError = null;
                string nativeClassStatus = value == null ? "not-applicable" : "not-il2cpp";
                if (value is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase nativeObject)
                {
                    IntPtr nativePointer = nativeObject.Pointer;
                    pointer = nativePointer.ToString("X");
                    if (nativePointer == IntPtr.Zero) nativeClassStatus = "null-pointer";
                    else try
                    {
                        IntPtr nativeClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(nativePointer);
                        if (nativeClass == IntPtr.Zero) nativeClassStatus = "null-class";
                        else
                        {
                            nativeClassName = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name_(nativeClass);
                            nativeClassNamespace = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_namespace_(nativeClass);
                            nativeClassStatus = string.IsNullOrWhiteSpace(nativeClassName) ? "unavailable" : "ok";
                        }
                    }
                    catch (Exception ex) { nativeClassStatus = "error"; nativeClassError = ex.GetType().FullName + ": " + ex.Message; }
                }
                return new { supported = true, available = value != null, status = value == null ? "null" : "available", proxyType = value?.GetType().FullName, pointer, nativeClassStatus, nativeClassName, nativeClassNamespace, nativeClassError };
            }
            catch (Exception ex)
            {
                return new { supported = true, available = false, status = "error", proxyType = (string?)null, pointer = (string?)null, nativeClassStatus = "unavailable", nativeClassName = (string?)null, nativeClassNamespace = (string?)null, nativeClassError = ex.GetType().FullName + ": " + ex.Message, error = ex.ToString() };
            }
        }

        private static object CaptureBrainValue(Func<object?> read)
        {
            try
            {
                object? value = read();
                return new { supported = true, available = value != null, status = value == null ? "null" : "available", type = value?.GetType().FullName, value = value?.ToString() };
            }
            catch (Exception error)
            {
                return new { supported = true, available = false, status = "error", type = (string?)null, value = (string?)null, error = error.ToString() };
            }
        }

        private static object CaptureDecisionLookup(BotOwner? bot, Assembly? bigBrainAssembly)
        {
            if (bot == null)
                return new { supported = true, available = false, status = "null-bot-owner" };

            try
            {
                Type wrapper = bigBrainAssembly?.GetType("DrakiaXYZ.BigBrain.Internal.CustomLayerWrapper", false)
                    ?? throw new TypeLoadException("BigBrain CustomLayerWrapper type unavailable.");
                MethodInfo lookup = wrapper.GetMethod(
                    "GetLogicInstance",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(BotOwner) },
                    null
                ) ?? throw new MissingMethodException(wrapper.FullName, "GetLogicInstance(BotOwner)");

                object? node = lookup.Invoke(null, new object[] { bot });
                if (node == null)
                    return new { supported = true, available = false, status = "null-result" };

                string? pointer = node is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase nativeNode
                    ? nativeNode.Pointer.ToString("X")
                    : null;
                return new { supported = true, available = true, status = "node-found", nodeType = node.GetType().FullName, pointer };
            }
            catch (TargetInvocationException error)
            {
                Exception cause = error.InnerException ?? error;
                return new { supported = true, available = false, status = "error", error = cause.ToString() };
            }
            catch (Exception error)
            {
                return new { supported = false, available = false, status = "error", error = error.ToString() };
            }
        }

        private sealed class BotEnemyTargetSnapshot
        {
            public bool Supported { get; init; }
            public bool Available { get; init; }
            public string Status { get; init; } = "unknown";
            public bool? HaveEnemy { get; init; }
            public string? EnemyPlayerPointer { get; init; }
            public string? EnemyPlayerProfileId { get; init; }
            public bool? TargetsMainPlayer { get; init; }
            public string? Error { get; init; }
            public object ToProbeObject() => new { supported = Supported, available = Available, status = Status, haveEnemy = HaveEnemy, enemyPlayerPointer = EnemyPlayerPointer, enemyPlayerProfileId = EnemyPlayerProfileId, targetsMainPlayer = TargetsMainPlayer, error = Error };
        }
        private static string? ReadNormalNativePointer(object? value, out string status)
        {
            Type? type = value?.GetType();
            if (type == null) { status = "null-native-object"; return null; }
            while (type != null)
            {
                PropertyInfo? property = type.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    object? raw = property.GetValue(value);
                    if (raw is not IntPtr pointer) { status = "unsupported-pointer-type"; return null; }
                    if (pointer == IntPtr.Zero) { status = "null-native-pointer"; return null; }
                    status = "available";
                    return pointer.ToString("X");
                }
                type = type.BaseType;
            }
            status = "pointer-member-unavailable";
            return null;
        }
        private static BotEnemyTargetSnapshot CaptureBotEnemyTarget(BotOwner? bot, string? mainPlayerProfileId)
        {
            if (bot == null) return new BotEnemyTargetSnapshot { Supported = true, Available = false, Status = "no-bot-owner" };
            try
            {
                object? memory = ReadNormalMember(bot, "Memory");
                if (memory == null) return new BotEnemyTargetSnapshot { Supported = true, Available = false, Status = "null-memory" };
                PropertyInfo? goalProperty = memory.GetType().GetProperty("GoalEnemy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (goalProperty == null || goalProperty.GetIndexParameters().Length != 0)
                    return new BotEnemyTargetSnapshot { Supported = false, Available = false, Status = "unsupported-goal-enemy-member", Error = memory.GetType().FullName };
                PropertyInfo? haveEnemyProperty = memory.GetType().GetProperty("HaveEnemy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                bool? haveEnemy = haveEnemyProperty?.GetValue(memory) is bool value ? value : null;
                object? goalEnemy = goalProperty.GetValue(memory);
                if (goalEnemy == null) return haveEnemy == true ? new BotEnemyTargetSnapshot { Supported = true, Available = false, Status = "goal-enemy-null-have-enemy-true", HaveEnemy = true, Error = "HaveEnemy is true while GoalEnemy is null." } : haveEnemy == false ? new BotEnemyTargetSnapshot { Supported = true, Available = true, Status = "no-goal-enemy", HaveEnemy = false } : new BotEnemyTargetSnapshot { Supported = true, Available = false, Status = "goal-enemy-null-have-enemy-unknown", HaveEnemy = null, Error = "HaveEnemy could not be read." };
                PropertyInfo? personProperty = goalEnemy.GetType().GetProperty("Person", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (personProperty == null || personProperty.GetIndexParameters().Length != 0)
                    return new BotEnemyTargetSnapshot { Supported = false, Available = false, Status = "unsupported-enemy-person-member", HaveEnemy = haveEnemy, Error = goalEnemy.GetType().FullName };
                object? enemyPlayer = personProperty.GetValue(goalEnemy);
                if (enemyPlayer == null) return new BotEnemyTargetSnapshot { Supported = true, Available = false, Status = "null-enemy-player", HaveEnemy = haveEnemy };
                PropertyInfo? profileProperty = enemyPlayer.GetType().GetProperty("ProfileId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string? profileId = profileProperty?.GetValue(enemyPlayer) as string;
                if (profileProperty == null) return new BotEnemyTargetSnapshot { Supported = false, Available = false, Status = "unsupported-enemy-profile-id-member", HaveEnemy = haveEnemy, Error = enemyPlayer.GetType().FullName };
                if (string.IsNullOrWhiteSpace(profileId)) return new BotEnemyTargetSnapshot { Supported = true, Available = false, Status = "enemy-profile-id-unavailable", HaveEnemy = haveEnemy };
                string? pointer = ReadNormalNativePointer(enemyPlayer, out string pointerStatus);
                return new BotEnemyTargetSnapshot { Supported = true, Available = true, Status = pointer == null ? "enemy-profile-available-pointer-" + pointerStatus : "current-goal-enemy", HaveEnemy = haveEnemy, EnemyPlayerPointer = pointer, EnemyPlayerProfileId = profileId, TargetsMainPlayer = !string.IsNullOrWhiteSpace(mainPlayerProfileId) ? string.Equals(profileId, mainPlayerProfileId, StringComparison.Ordinal) : null };
            }
            catch (Exception error) { return new BotEnemyTargetSnapshot { Supported = true, Available = false, Status = "read-error", Error = error.ToString() }; }
        }
        private static object CaptureMainPlayerTargetingBots(GameWorld? world, string? mainPlayerProfileId)
        {
            if (world?.AllAlivePlayersList == null) return new { supported = false, available = false, status = "null-alive-player-list", mainPlayerProfileId, botCount = (int?)null, targetingMainPlayerCount = (int?)null, unresolvedBotCount = (int?)null };
            if (string.IsNullOrWhiteSpace(mainPlayerProfileId)) return new { supported = false, available = false, status = "main-player-profile-id-unavailable", mainPlayerProfileId, botCount = (int?)null, targetingMainPlayerCount = (int?)null, unresolvedBotCount = (int?)null };
            int bots = 0, targeted = 0, unresolved = 0;
            var errors = new List<string>();
            try
            {
                foreach (Player? alive in world.AllAlivePlayersList)
                {
                    if (alive == null) continue;
                    try
                    {
                        if (!alive.IsAI) continue;
                        bots++;
                        BotEnemyTargetSnapshot state = CaptureBotEnemyTarget(alive.AIData?.BotOwner, mainPlayerProfileId);
                        if (!state.Supported || !state.Available) { unresolved++; if (state.Error != null) errors.Add(state.Status + ": " + state.Error); continue; }
                        if (state.TargetsMainPlayer == true) targeted++;
                    }
                    catch (Exception error)
                    {
                        unresolved++;
                        errors.Add("actor-read-error: " + error);
                    }
                }
            }
            catch (Exception error)
            {
                unresolved++;
                errors.Add("alive-player-enumeration-error: " + error);
            }
            return new { supported = unresolved == 0, available = true, status = unresolved == 0 ? "available" : "partial", mainPlayerProfileId, botCount = bots, targetingMainPlayerCount = targeted, unresolvedBotCount = unresolved, errors };
        }
        private static object CapturePlayerHeading(Player player)
        {
            const string source = "EFT.Player.MovementContext -> EFT.MovementContext.LookDirection/Rotation/Yaw/Pitch";
            try
            {
                var movementContext = player.MovementContext;
                if (movementContext == null)
                    return new { supported = true, available = false, status = "null-movement-context", source, lookDirection = (object?)null, rotation = (object?)null, yaw = (float?)null, pitch = (float?)null };
                var lookDirection = movementContext.LookDirection;
                var rotation = movementContext.Rotation;
                return new { supported = true, available = true, status = "available", source, lookDirection = new { x = lookDirection.x, y = lookDirection.y, z = lookDirection.z }, rotation = new { x = rotation.x, y = rotation.y }, yaw = movementContext.Yaw, pitch = movementContext.Pitch };
            }
            catch (Exception error)
            {
                return new { supported = true, available = false, status = "read-error", source, error = error.ToString() };
            }
        }
        private static object CaptureBigBrainDispatch(Assembly? assembly, IntPtr botPointer)
{
    if (assembly == null) return new { supported = false, available = false, status = "bigbrain-assembly-unavailable", error = (string?)null };
    if (botPointer == IntPtr.Zero) return new { supported = true, available = false, status = "bot-owner-unavailable", botPointer = "0", snapshot = (object?)null, error = (string?)null };
    try
    {
        Type registry = assembly.GetType("DrakiaXYZ.BigBrain.Internal.NativeBrainRegistry", false)
            ?? throw new TypeLoadException("NativeBrainRegistry unavailable.");
        MethodInfo snapshot = registry.GetMethod("GetDispatchSnapshotForBot", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null)
            ?? throw new MissingMethodException(registry.FullName, "GetDispatchSnapshotForBot(IntPtr)");
        object? dispatch = snapshot.Invoke(null, new object[] { botPointer });
        return new { supported = true, available = dispatch != null, status = dispatch == null ? "null-snapshot" : "available", botPointer = botPointer.ToString("X"), snapshot = dispatch, error = (string?)null };
    }
    catch (Exception error)
    {
        return new { supported = false, available = false, status = "bot-filtered-snapshot-unavailable", botPointer = botPointer.ToString("X"), snapshot = (object?)null, error = error.ToString() };
    }
}private static object CaptureBigBrainPatchRegistration(Assembly? bigBrainAssembly)
 {
 try
 {
 Assembly? gameAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
 Type? layerType = gameAssembly?.GetType("EFT.StandByLogicLayer", false);
 Assembly? harmonyAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "0Harmony");
 Type? harmonyType = harmonyAssembly?.GetType("HarmonyLib.Harmony", false);
 MethodInfo? getPatchInfo = harmonyType?.GetMethod("GetPatchInfo", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(MethodBase) }, null);
 if (layerType == null || getPatchInfo == null) return new { supported = false, available = false, status = "target-or-harmony-api-unavailable", error = (string?)null };
 return new { supported = true, available = true, shallUseNow = CaptureBigBrainPatchTarget(layerType, getPatchInfo, "ShallUseNow", "StandByShallUseNowPatch"), getDecision = CaptureBigBrainPatchTarget(layerType, getPatchInfo, "GetDecision", "StandByGetDecisionPatch"), error = (string?)null };
 }
 catch (Exception error) { return new { supported = true, available = false, status = "read-error", error = error.ToString() }; }
 }

 private static object CaptureBigBrainPatchTarget(Type layerType, MethodInfo getPatchInfo, string methodName, string patchName)
 {
 try
 {
 MethodInfo? target = layerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
 if (target == null) return new { available = false, status = "target-method-unavailable", prefixCount = (int?)null, ownPrefixRegistered = (bool?)null, error = (string?)null };
 object? info = getPatchInfo.Invoke(null, new object[] { target });
 if (info == null) return new { available = true, status = "no-patch-info", prefixCount = 0, ownPrefixRegistered = false, error = (string?)null };
 object? raw = info.GetType().GetProperty("Prefixes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(info);
 int count = 0;
 bool own = false;
 if (raw is IEnumerable prefixes)
 foreach (object? prefix in prefixes)
 {
 if (prefix == null) continue;
 count++;
 object? patchMethod = prefix.GetType().GetField("PatchMethod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(prefix)
 ?? prefix.GetType().GetProperty("PatchMethod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(prefix);
 string? declaring = (patchMethod as MethodInfo)?.DeclaringType?.FullName;
 if (declaring != null && declaring.Contains(patchName, StringComparison.Ordinal)) own = true;
 }
 return new { available = true, status = own ? "own-prefix-registered" : "own-prefix-not-registered", prefixCount = count, ownPrefixRegistered = own, error = (string?)null };
 }
 catch (Exception error) { return new { available = false, status = "read-error", prefixCount = (int?)null, ownPrefixRegistered = (bool?)null, error = error.ToString() }; }
 }
 private static object CaptureFollowerBossState(BotOwner? bot)
 {
 if (bot == null) return new { supported = true, available = false, status = "null-bot-owner" };
 try
 {
 object? follower = bot.BotFollower;
 bool haveBoss = bot.BotFollower?.HaveBoss == true;
 object? bossProxy = bot.BotFollower?.BossToFollow;
 Type? bossPlayersType = CoreType("pitTeam.Modules.BossPlayers");
 object? instance = bossPlayersType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
 MethodInfo? isFollowerMethod = bossPlayersType?.GetMethod("IsFollower", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BotOwner) }, null);
 MethodInfo? resolver = bossPlayersType?.GetMethod("GetResolvedBossForFollower", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BotOwner) }, null);
 bool? isFollower = isFollowerMethod?.Invoke(null, new object[] { bot }) as bool?;
 object? resolved = resolver?.Invoke(null, new object[] { bot });
 object? nativePointer = bossProxy == null ? null
 : bossProxy.GetType().GetProperty("Pointer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(bossProxy)
 ?? bossProxy.GetType().GetField("Pointer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(bossProxy);
 return new { supported = bossPlayersType != null, available = true, status = bossPlayersType == null ? "registry-unavailable" : "available", botState = bot.BotState.ToString(), isFollower, haveBoss, bossToFollowType = bossProxy?.GetType().FullName, bossToFollowPointer = nativePointer?.ToString(), registryResolvedType = resolved?.GetType().FullName, error = (string?)null };
 }
 catch (Exception error)
 {
 return new { supported = true, available = false, status = "read-error", error = (error is TargetInvocationException invocation ? invocation.InnerException ?? error : error).ToString() };
 }
 }
 private static object CapturePlayerObservation(Player player, string? mainPlayerProfileId)
        {
            var p = player.Position;
            object transformObservation;
            try
            {
                var transform = player.transform;
                if (transform == null)
                    transformObservation = new { supported = true, available = false, status = "null-transform", forward = (object?)null, rotation = (object?)null };
                else
                {
                    var forward = transform.forward;
                    var rotation = transform.rotation;
                    transformObservation = new { supported = true, available = true, status = "available", forward = new { x = forward.x, y = forward.y, z = forward.z }, rotation = new { x = rotation.x, y = rotation.y, z = rotation.z, w = rotation.w } };
                }
            }
            catch (Exception transformError)
            {
                transformObservation = new { supported = true, available = false, status = "read-error", error = transformError.ToString() };
            }
            string? botPointer = null;
            string? activeCustomLayer = null;
            string? activeCustomLogic = null;
            string? nativeDecision = null;
            BotOwner? bot = null;
            StandartBotBrain? botBrain = null;
            AICoreAgent<BotLogicDecision>? agent = null;
            BaseBrain? baseBrain = null;
            AICoreLayer<BotLogicDecision>? currentLayer = null;

            object nativeBotOwner = CaptureIl2CppReference(() => bot = player.AIData?.BotOwner);
            BotEnemyTargetSnapshot enemyTarget = CaptureBotEnemyTarget(bot, mainPlayerProfileId);
            if (bot != null)
            {
                try { botPointer = bot.Pointer.ToString("X"); }
                catch { /* pointer failure is represented by nativeBotOwner status */ }
            }
            object brainState = CaptureIl2CppReference(() => botBrain = bot?.Brain);
            object agentState = CaptureIl2CppReference(() => agent = botBrain?.Agent);
            object baseBrainState = CaptureIl2CppReference(() => baseBrain = botBrain?.BaseBrain);
            object currentLayerState = CaptureIl2CppReference(() => currentLayer = baseBrain?.CurLayerInfo);
            object? decisionValue = null;
            object currentDecision = CaptureBrainValue(() => decisionValue = agent?.LastResult().Action);
            nativeDecision = decisionValue?.ToString();

            Assembly? bigBrainAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "DrakiaXYZ-BigBrain");
            object decisionLookup = CaptureDecisionLookup(bot, bigBrainAssembly);
            object brainShortName = CaptureBrainShortName(baseBrain);
            object brainLayers = CaptureBrainLayerDictionary(baseBrain);
            object registeredBrainLayers = CaptureBigBrainLayerCatalog(bigBrainAssembly);
            object? layer = null;
            object? logic = null;
            bool brainApiSupported = false;
            string? brainApiError = null;
            try
            {
                Type manager = bigBrainAssembly?.GetType("DrakiaXYZ.BigBrain.Brains.BrainManager", false)
                    ?? throw new TypeLoadException("BigBrain BrainManager type unavailable.");
                MethodInfo layerMethod = manager.GetMethod("GetActiveLayer", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingMethodException(manager.FullName, "GetActiveLayer");
                MethodInfo logicMethod = manager.GetMethod("GetActiveLogic", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingMethodException(manager.FullName, "GetActiveLogic");
                layer = bot == null ? null : layerMethod.Invoke(null, new object[] { bot });
                logic = bot == null ? null : logicMethod.Invoke(null, new object[] { bot });
                activeCustomLayer = layer?.GetType().FullName;
                activeCustomLogic = logic?.GetType().FullName;
                brainApiSupported = true;
            }
            catch (TargetInvocationException error)
            {
                brainApiError = (error.InnerException ?? error).ToString();
            }
            catch (Exception error)
            {
                brainApiError = error.ToString();
            }

            object brain = new
            {
                supported = brainApiSupported,
                error = brainApiError,
                nativeBotOwner,
                brain = brainState,
                agent = agentState,
                baseBrain = baseBrainState,
                currentLayer = currentLayerState,
                currentDecision,
                decisionLookup,
                brainShortName,
                brainLayers,
                registeredBrainLayers,
 bigBrainDispatch = CaptureBigBrainDispatch(bigBrainAssembly, bot == null ? IntPtr.Zero : bot.Pointer),
 bigBrainPatchRegistration = CaptureBigBrainPatchRegistration(bigBrainAssembly),
 followerBossState = CaptureFollowerBossState(bot),
                activeCustomLayer,
                activeCustomLogic,
                nativeDecision
            };

            return new { profileId = player.ProfileId, nickname = player.Profile?.Info?.Nickname, isAI = player.IsAI, position = new { x = p.x, y = p.y, z = p.z }, transform = transformObservation, heading = CapturePlayerHeading(player), health = CaptureHealth(player), weapon = CaptureWeapon(player), enemyTarget = enemyTarget.ToProbeObject(), botPointer, activeCustomLayer, activeCustomLogic, nativeDecision, brain };
        }

        private static object CaptureBrainShortName(BaseBrain? brain)
        {
            if (brain == null) return new { supported = true, available = false, status = "null-brain", value = (string?)null, error = (string?)null };
            try
            {
                string? shortName = brain.ShortName();
                return new { supported = true, available = !string.IsNullOrWhiteSpace(shortName), status = string.IsNullOrWhiteSpace(shortName) ? "empty" : "ok", value = shortName, error = (string?)null };
            }
            catch (Exception ex)
            {
                return new { supported = true, available = false, status = "error", value = (string?)null, error = ex.ToString() };
            }
        }

        private static object CaptureBrainLayerDictionary(BaseBrain? brain)
        {
            if (brain == null) return new { supported = true, available = false, status = "null-brain", count = (int?)null, entries = (object?)null, error = (string?)null };
            try
            {
                var layers = brain._layers;
                if (layers == null) return new { supported = true, available = false, status = "null-dictionary", count = (int?)null, entries = (object?)null, error = (string?)null };
                var entries = new List<object>();
                foreach (var pair in layers)
                {
                    try
                    {
                        AICoreLayer<BotLogicDecision>? layer = pair.Value;
                        string? name = null;
                        string? entryError = null;
                        bool? active = null;
                        try
                        {
                            name = layer?.Name();
                            if (layer != null) active = brain.IsLayerActive(layer);
                        }
                        catch (Exception ex) { entryError = ex.GetType().FullName + ": " + ex.Message; }
                        entries.Add(new { id = pair.Key, name, active, layer = CaptureIl2CppReference(() => layer), error = entryError });
                    }
                    catch (Exception ex)
                    {
                        entries.Add(new { id = pair.Key, name = (string?)null, active = (bool?)null, layer = (object?)null, error = ex.ToString() });
                    }
                }
                return new { supported = true, available = true, status = "ok", count = layers.Count, entries = (object)entries, error = (string?)null };
            }
            catch (Exception ex)
            {
                return new { supported = true, available = false, status = "error", count = (int?)null, entries = (object?)null, error = ex.ToString() };
            }
        }

        private static object CaptureBigBrainLayerCatalog(Assembly? bigBrainAssembly)
        {
            if (bigBrainAssembly == null) return new { supported = false, available = false, status = "assembly-unavailable", count = (int?)null, entries = (object?)null, followerPatrolRegistration = "unknown", error = (string?)null };
            try
            {
                Type manager = bigBrainAssembly.GetType("DrakiaXYZ.BigBrain.Brains.BrainManager", false)
                    ?? throw new TypeLoadException("BigBrain BrainManager type unavailable.");
                FieldInfo? instanceField = manager.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
                if (instanceField == null) return new { supported = false, available = false, status = "instance-field-unavailable", count = (int?)null, entries = (object?)null, followerPatrolRegistration = "unknown", error = (string?)null };
                object? instance = instanceField.GetValue(null);
                if (instance == null) return new { supported = true, available = false, status = "manager-not-initialized", count = (int?)null, entries = (object?)null, followerPatrolRegistration = "not-observed", error = (string?)null };
                FieldInfo? layersField = manager.GetField("CustomLayers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (layersField == null) return new { supported = false, available = false, status = "catalog-field-unavailable", count = (int?)null, entries = (object?)null, followerPatrolRegistration = "unknown", error = (string?)null };
                object? rawLayers = layersField.GetValue(instance);
                if (rawLayers is not System.Collections.IDictionary layers)
                    return new { supported = false, available = rawLayers != null, status = rawLayers == null ? "null-catalog" : "unsupported-dictionary", count = (int?)null, entries = (object?)null, followerPatrolRegistration = "unknown", error = (string?)null };
                var entries = new List<object>();
                int followerMatches = 0;
                foreach (System.Collections.DictionaryEntry pair in layers)
                {
                    object? info = pair.Value;
                    Type? infoType = info?.GetType();
                    object? typeValue = infoType?.GetField("customLayerType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(info)
                        ?? infoType?.GetProperty("customLayerType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(info);
                    string? layerTypeName = (typeValue as Type)?.FullName;
                    if (string.Equals(layerTypeName, "pitTeam.BigBrain.FollowerPatrolLayer", StringComparison.Ordinal)) followerMatches++;
                    entries.Add(new { id = pair.Key?.ToString(), customLayerType = layerTypeName, infoAvailable = info != null, typeAvailable = typeValue is Type });
                }
                return new { supported = true, available = true, status = "ok", count = layers.Count, entries = (object)entries, followerPatrolRegistration = followerMatches == 1 ? "one" : followerMatches == 0 ? "none" : "multiple", error = (string?)null };
            }
            catch (Exception ex)
            {
                return new { supported = true, available = false, status = "error", count = (int?)null, entries = (object?)null, followerPatrolRegistration = "unknown", error = ex.ToString() };
            }
        }

        private static object CaptureRadioSoundClip(object radioSound, string fieldName)
        {
            try
            {
                FieldInfo? field = radioSound.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) return new { supported = false, available = false, error = "Field not found: " + fieldName };
                if (field.FieldType != typeof(AudioClip)) return new { supported = false, available = false, error = "Field is not AudioClip: " + fieldName };
                var clip = field.GetValue(radioSound) as AudioClip;
                bool referencePresent = !ReferenceEquals(clip, null);
                bool unityObjectAlive = clip != null;
                string? pointer = null;
                string? loadState = null;
                string? hideFlags = null;
                bool? unityReadyToPlay = null;
                bool? isReadyToPlay = null;
                float? length = null;
                int? samples = null;
                int? channels = null;
                int? frequency = null;
                string? loadType = null;
                if (referencePresent)
                {
                    try { pointer = clip!.Pointer.ToString("X"); }
                    catch { }
                }
                if (unityObjectAlive)
                {
                    try
                    {
                        loadState = clip!.loadState.ToString();
                        unityReadyToPlay = clip.isReadyToPlay;
                        length = clip.length;
                        samples = clip.samples;
                        channels = clip.channels;
                        frequency = clip.frequency;
                        loadType = clip.loadType.ToString();
                        hideFlags = clip.hideFlags.ToString();
                        isReadyToPlay = unityReadyToPlay == true
                            && string.Equals(loadState, "Loaded", StringComparison.Ordinal)
                            && length.GetValueOrDefault() > 0 && samples.GetValueOrDefault() > 0 && channels.GetValueOrDefault() > 0 && frequency.GetValueOrDefault() > 0
                            && !string.Equals(loadType, "Streaming", StringComparison.Ordinal);
                    }
                    catch { }
                }
                string? healthStatus = !referencePresent ? "reference-null"
                    : !unityObjectAlive ? "unity-fake-null"
                    : isReadyToPlay == true ? "ready"
                    : "loaded-content-unavailable-or-invalid";
                return new { supported = true, available = true, loaded = unityObjectAlive, referencePresent, unityObjectAlive, pointer, loadState, unityReadyToPlay, isReadyToPlay, length, samples, channels, frequency, loadType, hideFlags, healthStatus };
            }
            catch (Exception error)
            {
                return new { supported = true, available = false, error = error.ToString() };
            }
        }

        private static object CaptureRadioSoundInitialization()
        {
            Assembly? core = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "pitFireTeam");
            if (core == null) return new { supported = false, available = false, error = "pitFireTeam assembly is not loaded." };
            Type? pingType = core.GetType("pitTeam.Utils.PingTeamates", false);
            if (pingType == null) return new { supported = false, available = false, error = "PingTeamates type is not loaded." };
            FieldInfo? radioField = pingType.GetField("radioSound", BindingFlags.Static | BindingFlags.NonPublic);
            if (radioField == null) return new { supported = false, available = false, error = "PingTeamates.radioSound field is unavailable." };
            object? radioSound;
            try { radioSound = radioField.GetValue(null); }
            catch (Exception error) { return new { supported = true, available = false, error = error.ToString() }; }
            if (radioSound == null) return new { supported = true, available = false, error = "PingTeamates.radioSound is null." };
            object ownerState;
            try
            {
                var component = radioSound as Component;
                bool nativeAlive = component != null;
                object? loadDiagnostics = radioSound.GetType().GetMethod("GetLoadProbeSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(radioSound, null);
                ownerState = new { referencePresent = !ReferenceEquals(component, null), unityObjectAlive = nativeAlive, pointer = ReferenceEquals(component, null) ? null : component!.Pointer.ToString("X"), gameObjectAlive = nativeAlive ? (bool?)(component!.gameObject != null) : null, loadDiagnostics };
            }
            catch (Exception error) { ownerState = new { available = false, error = error.ToString() }; }
            object taskState;
            try
            {
                FieldInfo? taskField = radioSound.GetType().GetField("enableTask", BindingFlags.Instance | BindingFlags.NonPublic);
                if (taskField == null) taskState = new { supported = false, available = false, error = "RadioSound.enableTask field is unavailable." };
                else if (taskField.GetValue(radioSound) is not Task task) taskState = new { supported = true, available = true, started = false, status = "not-started", isCompleted = false, isFaulted = false, isCanceled = false };
                else taskState = new { supported = true, available = true, started = true, status = task.Status.ToString(), isCompleted = task.IsCompleted, isFaulted = task.IsFaulted, isCanceled = task.IsCanceled };
            }
            catch (Exception error) { taskState = new { supported = true, available = false, error = error.ToString() }; }
            object observedState;
            try
            {
                FieldInfo? observedField = radioSound.GetType().GetField("enableTaskObserved", BindingFlags.Instance | BindingFlags.NonPublic);
                observedState = observedField == null ? (object)new { supported = false, available = false, error = "RadioSound.enableTaskObserved field is unavailable." } : new { supported = true, available = true, value = observedField.GetValue(radioSound) };
            }
            catch (Exception error) { observedState = new { supported = true, available = false, error = error.ToString() }; }
            return new
            {
                supported = true,
                available = true,
                task = taskState,
                taskObserved = observedState,
                owner = ownerState,
                radioClip = CaptureRadioSoundClip(radioSound, "radioClip"),
                locationClip = CaptureRadioSoundClip(radioSound, "locationClip"),
                radioBeepClip = CaptureRadioSoundClip(radioSound, "radioBeepClip")
            };
        }

        internal static object SetQaPlayerProtectionEnabled(bool enable, bool fullHealthMaintenance = true)
        {
            try
            {
                Type? type = System.AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("pitTeam.RuntimeHarness.QaPlayerProtection", false))
                    .FirstOrDefault(candidate => candidate != null);
                if (type == null) return new { passed = false, enable, error = "runtime-harness-type-unavailable" };
                System.Reflection.MethodInfo? setter = type.GetMethod("SetEnabled", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool), typeof(bool) }, null);
                return setter?.Invoke(null, new object[] { enable, fullHealthMaintenance }) ?? (object)new { passed = false, enable, fullHealthMaintenance, error = "set-enabled-method-unavailable" };
            }
            catch (Exception error) { return new { passed = false, enable, error = error.ToString() }; }
        }
        internal static object CaptureQaPlayerProtection()
        {
            try
            {
                Type? type = System.AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("pitTeam.RuntimeHarness.QaPlayerProtection", false))
                    .FirstOrDefault(candidate => candidate != null);
                if (type == null) return new { requested = false, guard = "runtime-harness-type-unavailable", applied = false };
                System.Reflection.MethodInfo? snapshot = type.GetMethod("Snapshot", BindingFlags.Static | BindingFlags.Public);
                return snapshot?.Invoke(null, null) ?? (object)new { requested = false, guard = "snapshot-method-unavailable", applied = false };
            }
            catch (Exception error) { return new { requested = false, guard = "snapshot-error", applied = false, error = error.ToString() }; }
        }
        private static object CaptureRaidTimeObservation()
        {
            const string source = "EFT.AbstractGame.GameTimer.SessionTime/PastTime";
            try
            {
                if (!Singleton<AbstractGame>.Instantiated)
                    return new { supported = true, available = false, status = "no-active-game", source };
                var timer = Singleton<AbstractGame>.Instance?.GameTimer;
                if (timer == null)
                    return new { supported = true, available = false, status = "game-timer-null", source };
                var session = timer.SessionTime;
                double? sessionSeconds = session.HasValue ? session.Value.TotalSeconds : (double?)null;
                double pastSeconds = timer.PastTime.TotalSeconds;
                return new
                {
                    supported = true, available = true, status = "native-observed-not-ui-verified", source,
                    sessionSeconds, pastSeconds,
                    estimatedRemainingSeconds = sessionSeconds.HasValue ? sessionSeconds.Value - pastSeconds : (double?)null,
                    uiVerified = false
                };
            }
            catch (Exception error)
            {
                return new { supported = true, available = false, status = "timer-read-failed", source, error = error.GetType().Name + ": " + error.Message };
            }
        }

        internal static object ReportInstallStatus() => CaptureNormalPatchSnapshot("pitTeam.Compat.Spt5RuntimeInstallProbe", "ReportStatus");
        internal static object ReportInstallLootBody() => CaptureNormalPatchSnapshot("pitTeam.Compat.Spt5RuntimeInstallProbe", "LootBody");
        internal static object RunDoorCrossingSmoke() => CaptureNormalPatchSnapshot("pitTeam.Compat.NativeAiGeometry", "RunProbe");
        internal static object RunInstallCommand(string command) => CaptureNormalPatchSnapshot("pitTeam.Compat.Spt5RuntimeInstallProbe", "Command", new object[] { command });
        internal static object CaptureAppearanceOptions() => CaptureNormalPatchSnapshot("pitTeam.Patches.AddTeammateHeadSelectionOptionsPatch", "GetProbeSnapshot");
        internal static object CaptureProfileUiValues() => CaptureNormalPatchSnapshot("pitTeam.Patches.OtherPlayerProfileScreenPatch", "GetNativeProfileValues");
        internal static object CaptureBuildUiValues() => CaptureNormalPatchSnapshot("pitTeam.Patches.TeammateEquipmentBuildsScreenFlow", "GetNativePricingValues");
        internal static object QuickMoveEditorItem(EFT.UI.DragAndDrop.ItemView view) => CaptureNormalPatchSnapshot("pitTeam.Patches.OtherPlayerProfileScreenPatch", "QuickMoveLoadoutItem", new object[] { view });
        internal static object ExtractWithInstalledQaMod()
        {
            ManagedFramePump.RequireOwnerThread();
            var world = Singleton<GameWorld>.Instance;
            var player = world?.MainPlayer;
            if (!string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_SHADOW_MODE"), "true", StringComparison.OrdinalIgnoreCase)
                || !Uri.TryCreate(Environment.GetEnvironmentVariable("PIT_SPT5_BACKEND_URL"), UriKind.Absolute, out var backend)
                || backend.Scheme != Uri.UriSchemeHttps || !backend.IsLoopback || backend.Port != 6971
                || player?.ProfileId is not ("6ace6340c399ad84d0a90502" or "6ad49565f2713c7c5c904b23")
                || !Singleton<AbstractGame>.Instantiated)
                throw new InvalidOperationException("Instant extraction QA requires the fixed private shadow raid.");
            var assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "SPTExfilNow")
                ?? throw new InvalidOperationException("The user's SPTExfilNow test helper is not loaded.");
            var type = assembly.GetType("SPTExfilNow.ExfilHotkeyPatch", true)!;
            var method = type.GetMethod("StopRaid", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(GameWorld), typeof(Player) }, null);
            if (method == null || method.ReturnType != typeof(bool)) throw new MissingMethodException("Verified SPTExfilNow.StopRaid(GameWorld,Player) boundary changed.");
            bool requested = (bool)method.Invoke(null, new object[] { world!, player! })!;
            return new { passed = requested, helper = "user-installed-SPTExfilNow", profileId = player!.ProfileId, normalSettlementReadbackRequired = true };
        }
        internal static object CaptureInstallSample()
        {
            ManagedFramePump.RequireOwnerThread();
            var world = Singleton<GameWorld>.Instance;
            var player = GamePlayerOwner.MyPlayer;
            var rows = new List<object>();
            object Row(Player observed)
            {
                var position = observed.Position;
                return new { profileId = observed.ProfileId, isAI = observed.IsAI, position = new { x = position.x, y = position.y, z = position.z }, damageCoeff = observed.ActiveHealthController?.DamageCoeff, health = CaptureHealth(observed), enemyTarget = CaptureBotEnemyTarget(observed.AIData?.BotOwner, player?.ProfileId).ToProbeObject() };
            }
            if (world?.AllAlivePlayersList != null) foreach (var alive in world.AllAlivePlayersList) if (alive != null) rows.Add(Row(alive));
            return new { inRaid = Singleton<AbstractGame>.Instantiated && world != null && player != null, frame = Time.frameCount, timestamp = DateTimeOffset.UtcNow, realtime = Time.realtimeSinceStartup, playerProfileId = player?.ProfileId, mainPlayer = player == null ? null : Row(player), followers = Followers(), followerProfiles = CaptureFollowerProfiles(), players = rows, installObservation = CaptureNormalPatchSnapshot("pitTeam.Compat.Spt5RuntimeInstallProbe", "GetProbeSnapshot"), qaPlayerProtection = CaptureQaPlayerProtection() };
        }
        internal static object Capture()
        {
            ManagedFramePump.RequireOwnerThread();
            var world = Singleton<GameWorld>.Instance;
            var player = GamePlayerOwner.MyPlayer;

            Vector3? mainPosition = null;
            object mainPositionObservation = player == null
                ? new { supported = true, available = false, status = "main-player-unavailable", error = "GamePlayerOwner.MyPlayer is null." }
                : CaptureWorldPosition(() => { var transform = player.transform; if (transform == null) throw new InvalidOperationException("Main-player transform is null."); return transform.position; }, out mainPosition);
            object mainPlayerInteraction = CaptureMainPlayerInteraction(player, mainPosition, world != null);
            var rows = new List<object>();
            if (world?.AllAlivePlayersList != null)
                foreach (var alive in world.AllAlivePlayersList)
                    if (alive != null) rows.Add(CapturePlayerObservation(alive, player?.ProfileId));

            return new
            {
                inRaid = Singleton<AbstractGame>.Instantiated && world != null && player != null,
                frame = Time.frameCount,
                timestamp = DateTimeOffset.UtcNow,
                realtime = Time.realtimeSinceStartup,
                installObservation = CaptureNormalPatchSnapshot("pitTeam.Compat.Spt5RuntimeInstallProbe", "GetProbeSnapshot"),
                coreAvailable = CoreType("pitTeam.pitFireTeam") != null,
                playerProfileId = player?.ProfileId,
                mainPlayer = player == null ? (object)new { available = false, error = "GamePlayerOwner.MyPlayer is null." } : CapturePlayerObservation(player, player.ProfileId),
                mainPlayerPosition = mainPositionObservation,
                mainPlayerInteraction,
                followers = Followers(),
                followerProfiles = CaptureFollowerProfiles(),
                players = rows,
                mainPlayerTargetingBots = CaptureMainPlayerTargetingBots(world, player?.ProfileId),
                corpses = world == null ? (object)new { supported = true, available = false, count = (int?)null, scope = "GameWorld.ObservedPlayersCorpses only; does not represent all local loot corpses.", error = "GameWorld is null.", observedPlayersCorpses = new { supported = true, available = false, count = (int?)null, scope = "GameWorld.ObservedPlayersCorpses only; does not represent all local loot corpses.", error = "GameWorld is null." }, localLootCorpses = new { supported = true, available = false, collectionCount = (int?)null, scannedCount = 0, corpseCount = (int?)null, truncated = false, sampleTruncated = false, samples = new object[0], error = "GameWorld is null." } } : CaptureCorpses(world, player, mainPosition),
                exfiltration = world == null ? (object)new { supported = true, available = false, points = (object?)null, error = "GameWorld is null." } : CaptureExits(world, player),
                nativeConstructorHooks = CaptureNativeConstructorHooks(),
                aiDataBossBinding = CaptureBossAIDataBinding(),
                normalSquadPipeline = CaptureNormalSquadPipeline(),
                raidTimeObservation = CaptureRaidTimeObservation(),
                nativeGcCompatibility = Spt5NativeGcCompatibility.Snapshot(),
                bodyLootCommandObservation = Spt5BodyLootDiagnostics.Snapshot(),
                lootPatrolFaultObservation = pitTeam.Compat.Spt5LootPatrolDiagnostics.Snapshot(),
                qaPlayerProtection = CaptureQaPlayerProtection(),
                radioSoundInitialization = CaptureRadioSoundInitialization()
            };
        }
        internal static async Task<object> SpawnFollower()
        {
            ManagedFramePump.RequireOwnerThread();
            if (!Singleton<AbstractGame>.Instantiated || Singleton<GameWorld>.Instance == null
                || GamePlayerOwner.MyPlayer == null)
                throw new InvalidOperationException("Follower spawn requires an active native raid and main player.");
            var core = CoreType("pitTeam.pitFireTeam") ?? throw new InvalidOperationException("pitFireTeam is not loaded.");
            var method = core.GetMethod("SpawnFollowerConsoleCommandAsync", BindingFlags.Static | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null) ?? throw new MissingMethodException(core.FullName, "SpawnFollowerConsoleCommandAsync");
            int before = Followers();
            var task = method.Invoke(null, null) as Task ?? throw new InvalidOperationException("Spawn entry did not return its business Task.");
            await task;
            ManagedFramePump.RequireOwnerThread();
            var completion = new TaskCompletionSource<bool>();
            float deadline = Time.realtimeSinceStartup + 30f;
            ManagedFramePump.Enqueue(() =>
            {
                try
                {
                    if (Followers() > before) { completion.TrySetResult(true); return true; }
                    if (Time.realtimeSinceStartup >= deadline) { completion.TrySetResult(false); return true; }
                    return false;
                }
                catch (Exception error) { completion.TrySetException(error); return true; }
            });
            bool confirmed = await completion.Task;
            return new { passed = confirmed, phase = confirmed ? "follower-registered" : "task-returned-without-follower",
                followersBefore = before, followersAfter = Followers(), world = Capture() };
        }
    }
}
