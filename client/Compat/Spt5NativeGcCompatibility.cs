using System;
using System.Globalization;
using Comfort.Common;
using EFT;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Scripting;

namespace pitTeam.Compat
{
    internal static class Spt5NativeGcCompatibility
    {
        private const float CheckIntervalSeconds = 5f;
        private static float _nextCheckAt;
        private static bool? _configEnabled;
        private static bool _liveRaid;
        private static string? _lastCheckUtc;
        private static string? _lastBefore;
        private static string? _lastAfter;
        private static bool? _nativeDisabled;
        private static int _changeCount;
        private static string? _lastError;
        private static bool _firstErrorLogged;

        internal static void Tick(bool configEnabled)
        {
            _configEnabled = configEnabled;
            if (!configEnabled) return;

            try
            {
                float now = Time.realtimeSinceStartup;
                if (now < _nextCheckAt) return;
                _nextCheckAt = now + CheckIntervalSeconds;
                _lastCheckUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                _liveRaid = false;

                if (!Singleton<AbstractGame>.Instantiated) return;
                GameWorld? world = Singleton<GameWorld>.Instance;
                Player? mainPlayer = world?.MainPlayer;
                if (world == null || mainPlayer == null)
                    return;

                _liveRaid = true;
                _lastError = null;
                string before = GarbageCollector.GCMode.ToString();
                bool disabledBefore = IL2CPP.il2cpp_gc_is_disabled();
                _lastBefore = before;
                _lastAfter = before;
                _nativeDisabled = disabledBefore;

                if (!string.Equals(before, "Enabled", StringComparison.Ordinal))
                {
                    GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                    string after = GarbageCollector.GCMode.ToString();
                    bool disabledAfter = IL2CPP.il2cpp_gc_is_disabled();
                    _lastAfter = after;
                    _nativeDisabled = disabledAfter;
                    bool changed = !string.Equals(before, after, StringComparison.Ordinal) || disabledBefore != disabledAfter;
                    if (changed)
                    {
                        _changeCount++;
                        try { UnityEngine.Debug.Log("[Compat][Il2CppAutomaticGC] mode=" + before + "->" + after + " il2cppDisabled=" + disabledBefore + "->" + disabledAfter); }
                        catch { }
                    }
                    if (!string.Equals(after, "Enabled", StringComparison.Ordinal) || disabledAfter)
                        throw new InvalidOperationException("GC enable readback failed: mode=" + after + ", il2cppDisabled=" + disabledAfter);
                }
                else if (disabledBefore)
                {
                    throw new InvalidOperationException("Unity GCMode is Enabled while IL2CPP reports GC disabled; no mode write was issued.");
                }
            }
            catch (Exception error)
            {
                _lastError = LimitError(error);
                if (!_firstErrorLogged)
                {
                    _firstErrorLogged = true;
                    try { UnityEngine.Debug.LogError("[Compat][Il2CppAutomaticGC] " + _lastError); }
                    catch { }
                }
            }
        }

        internal static object Snapshot()
        {
            return new
            {
                configEnabled = _configEnabled,
                liveRaid = _liveRaid,
                lastCheckUtc = _lastCheckUtc,
                lastBefore = _lastBefore,
                lastAfter = _lastAfter,
                nativeDisabled = _nativeDisabled,
                changeCount = _changeCount,
                lastError = _lastError
            };
        }

        private static string LimitError(Exception error)
        {
            string value = error.GetType().Name + ": " + error.Message;
            return value.Length <= 1024 ? value : value.Substring(0, 1024);
        }
    }
}
