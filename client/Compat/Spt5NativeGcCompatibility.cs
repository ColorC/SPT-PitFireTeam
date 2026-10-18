using System;
using System.Diagnostics;
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
        private const ulong IncrementalSliceNanoseconds = 1_000_000;
        private const int IncrementalLimitSeconds = 30;
        private static bool _incrementalActive, _restorePending, _restored;
        private static GarbageCollector.Mode? _originalMode, _restoredMode;
        private static IntPtr _experimentWorld, _experimentPlayer;
        private static int _experimentThread, _lastSliceFrame = int.MinValue, _restoreAttempts;
        private static long _deadlineTicks, _nextRestoreAttempt, _sliceCalls, _sliceTotalTicks, _sliceMaxTicks;
        private static DateTime? _experimentStartedUtc, _experimentDeadlineUtc;
        private static bool? _incrementalSupported, _nativeDisabledBefore, _nativeDisabledManual, _nativeDisabledRestored, _lastSliceMoreWork;
        private static string? _experimentError, _restoreReason, _restoreError;

        internal static void Tick(bool configEnabled)
        {
            _configEnabled = configEnabled;
            // A private, explicitly requested experiment owns GCMode temporarily.
            // Outside that bounded interval the original Enabled policy is unchanged.
            if (TickQaIncremental()) return;
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
                lastError = _lastError,
                qaIncremental = IncrementalSnapshot()
            };
        }

        internal static void BeginQaIncremental(GameWorld world, Player mainPlayer)
        {
            ManagedFramePump.RequireOwnerThread();
            if (_incrementalActive) throw new InvalidOperationException("Incremental QA is already active; its deadline cannot be extended.");
            if (_restorePending && !EndQaIncremental("restart-after-pending-restore"))
                throw new InvalidOperationException("Previous GC mode restoration is still pending.");
            // Apply() owns the private endpoint/profile guards. Cache native identities,
            // never the ProfileId property on the per-frame path.
            if (world.Pointer == IntPtr.Zero || mainPlayer.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Incremental QA requires non-null native world/player identities.");
            if (!GarbageCollector.isIncremental)
                throw new NotSupportedException("This runtime does not report incremental GC support.");
            GarbageCollector.Mode original = GarbageCollector.GCMode;
            bool nativeBefore = IL2CPP.il2cpp_gc_is_disabled();
            _originalMode = original;
            _experimentWorld = world.Pointer;
            _experimentPlayer = mainPlayer.Pointer;
            _experimentThread = Environment.CurrentManagedThreadId;
            _experimentStartedUtc = DateTime.UtcNow;
            _experimentDeadlineUtc = _experimentStartedUtc.Value.AddSeconds(IncrementalLimitSeconds);
            _deadlineTicks = Stopwatch.GetTimestamp() + IncrementalLimitSeconds * Stopwatch.Frequency;
            _sliceCalls = _sliceTotalTicks = _sliceMaxTicks = 0;
            // Keep the last slice frame across experiments too: stop/start commands
            // in one Unity frame must not buy a second slice in that same frame.
            _restoreAttempts = 0;
            _restored = false;
            _restoredMode = null;
            _restoreReason = _restoreError = _experimentError = null;
            _nativeDisabledBefore = nativeBefore;
            _nativeDisabledManual = _nativeDisabledRestored = _lastSliceMoreWork = null;
            _incrementalSupported = true;
            _restorePending = true; // even a partially failing setter must be rolled back
            try
            {
                GarbageCollector.GCMode = GarbageCollector.Mode.Manual;
                if (GarbageCollector.GCMode != GarbageCollector.Mode.Manual || !GarbageCollector.isIncremental)
                    throw new InvalidOperationException("Manual mode/incremental support readback failed.");
                _nativeDisabledManual = IL2CPP.il2cpp_gc_is_disabled(); // observation, no Manual == Disabled assumption
                _restorePending = false;
                _incrementalActive = true;
            }
            catch (Exception error)
            {
                _experimentError = LimitError(error);
                EndQaIncremental("start-error");
                throw;
            }
        }

        internal static bool EndQaIncremental(string reason)
        {
            if (!_incrementalActive && !_restorePending) return true;
            _incrementalActive = false;
            _restorePending = true;
            _restoreReason = reason;
            return TryRestoreIncremental();
        }

        private static bool TickQaIncremental()
        {
            if (!_incrementalActive && !_restorePending) return false;
            if (Environment.CurrentManagedThreadId != _experimentThread)
            {
                _experimentError = "GC experiment tick arrived on a different thread; restoration is pending on its owner.";
                _incrementalActive = false;
                _restorePending = true;
                _restoreReason = "owner-thread-changed";
                return true;
            }
            if (_restorePending)
            {
                if (Stopwatch.GetTimestamp() >= _nextRestoreAttempt) TryRestoreIncremental();
                return true;
            }
            try
            {
                if (Stopwatch.GetTimestamp() >= _deadlineTicks)
                {
                    EndQaIncremental("timeout-30s");
                    return true;
                }
                GameWorld? world = Singleton<GameWorld>.Instance;
                Player? main = world?.MainPlayer;
                if (!Singleton<AbstractGame>.Instantiated || world?.Pointer != _experimentWorld || main?.Pointer != _experimentPlayer
                    || main?.HealthController?.IsAlive != true)
                {
                    EndQaIncremental("world-or-main-player-changed");
                    return true;
                }
                if (GarbageCollector.GCMode != GarbageCollector.Mode.Manual)
                    throw new InvalidOperationException("Incremental experiment lost Manual mode.");
                _incrementalSupported = GarbageCollector.isIncremental;
                if (_incrementalSupported != true)
                    throw new InvalidOperationException("Incremental experiment lost incremental support.");
                int frame = Time.frameCount;
                if (frame == _lastSliceFrame) return true;
                _lastSliceFrame = frame;
                long before = Stopwatch.GetTimestamp();
                _sliceCalls++;
                try { _lastSliceMoreWork = GarbageCollector.CollectIncremental(IncrementalSliceNanoseconds); }
                finally
                {
                    long elapsed = Stopwatch.GetTimestamp() - before;
                    _sliceTotalTicks += elapsed;
                    if (elapsed > _sliceMaxTicks) _sliceMaxTicks = elapsed;
                }
                if (Stopwatch.GetTimestamp() >= _deadlineTicks) EndQaIncremental("timeout-30s");
            }
            catch (Exception error)
            {
                _experimentError = LimitError(error);
                EndQaIncremental("slice-or-state-error");
            }
            return true;
        }

        private static bool TryRestoreIncremental()
        {
            if (Environment.CurrentManagedThreadId != _experimentThread || !_originalMode.HasValue) return false;
            _restoreAttempts++;
            _nextRestoreAttempt = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            try
            {
                GarbageCollector.GCMode = _originalMode.Value;
                GarbageCollector.Mode after = GarbageCollector.GCMode;
                _restoredMode = after;
                if (after != _originalMode.Value) throw new InvalidOperationException("Original GCMode restoration readback differs.");
                _restorePending = false;
                _restored = true;
                _restoreError = null;
                try { _nativeDisabledRestored = IL2CPP.il2cpp_gc_is_disabled(); }
                catch (Exception error) { _restoreError = "Native disabled observation: " + LimitError(error); }
                try { _nextCheckAt = Time.realtimeSinceStartup + CheckIntervalSeconds; }
                catch { _nextCheckAt = 0f; }
                return true;
            }
            catch (Exception error)
            {
                _restoreError = LimitError(error);
                _restored = false;
                return false; // no more slices; retry at most once/second on the owner
            }
        }

        internal static object IncrementalSnapshot() => new
        {
            active = _incrementalActive, restorePending = _restorePending, restored = _restored,
            originalMode = _originalMode?.ToString(), restoredMode = _restoredMode?.ToString(),
            supported = _incrementalSupported, startedUtc = _experimentStartedUtc, deadlineUtc = _experimentDeadlineUtc,
            deadlineStopwatchTicks = _deadlineTicks, stopwatchFrequency = Stopwatch.Frequency,
            durationLimitSeconds = IncrementalLimitSeconds, sliceBudgetNanoseconds = IncrementalSliceNanoseconds,
            sliceCalls = _sliceCalls, totalSliceStopwatchTicks = _sliceTotalTicks, maxSliceStopwatchTicks = _sliceMaxTicks,
            totalSliceMilliseconds = _sliceTotalTicks * (1000d / Stopwatch.Frequency),
            maxSliceMilliseconds = _sliceMaxTicks * (1000d / Stopwatch.Frequency),
            lastSliceMoreWork = _lastSliceMoreWork, lastSliceFrame = _lastSliceFrame,
            nativeDisabledBefore = _nativeDisabledBefore, nativeDisabledManual = _nativeDisabledManual,
            nativeDisabledRestored = _nativeDisabledRestored, restoreAttempts = _restoreAttempts,
            restoreReason = _restoreReason, error = _experimentError, restoreError = _restoreError,
            performanceAcceptance = false
        };

        private static string LimitError(Exception error)
        {
            string value = error.GetType().Name + ": " + error.Message;
            return value.Length <= 1024 ? value : value.Substring(0, 1024);
        }
    }
}
