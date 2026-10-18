using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using Il2CppInterop.Runtime;
using UnityEngine.Scripting;

namespace pitTeam.Compat
{
    internal static class Spt5QaNativeGcControl
    {
        private const string MainProfileId = "6ace6340c399ad84d0a90502";
        private const string WedgeProfileId = "6ad49565f2713c7c5c904b23";
        private static object? _savedOriginal;
        private static string? _savedOriginalName;
        private static bool? _savedOriginalDisabled;

        internal static object Apply(string? mode)
        {
            PropertyInfo? property = null;
            object? beforeValue = null;
            bool? savedDisabled = _savedOriginalDisabled;
            string? before = null, after = null, error = null;
            bool? disabledBefore = null, disabledAfter = null;
            bool incrementalStarted = false;
            string stage = "mode-validation";
            try
            {
                if (mode != "Enabled" && mode != "Disabled" && mode != "Restore" && mode != "Incremental")
                    throw new ArgumentException("Mode must be Enabled, Disabled, Restore, or Incremental.", nameof(mode));
                stage = "private-shadow-guard";
                if (!string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_SHADOW_MODE"), "true", StringComparison.OrdinalIgnoreCase)
                    || !Uri.TryCreate(Environment.GetEnvironmentVariable("PIT_SPT5_BACKEND_URL"), UriKind.Absolute, out Uri? backend)
                    || backend.Scheme != Uri.UriSchemeHttps || !backend.IsLoopback || backend.Port != 6971)
                    throw new InvalidOperationException("GC mode control requires PIT_SPT5_SHADOW_MODE=true and HTTPS loopback backend port 6971.");
                if (mode == "Incremental")
                {
                    string businessMode = Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_MODE") ?? "normal";
                    if (string.Equals(businessMode, "observe-core", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(businessMode, "observe-ctor", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Incremental QA requires the normal PFT Update driver; observation business modes do not tick it.");
                }
                ManagedFramePump.RequireOwnerThread();
                stage = "main-player-guard";
                GameWorld? world = Singleton<GameWorld>.Instance;
                Player? main = world?.MainPlayer;
                if (!Singleton<AbstractGame>.Instantiated || world == null || main == null || main.HealthController?.IsAlive != true)
                    throw new InvalidOperationException("GC mode control requires a living MainPlayer during an active raid.");
                string profileId = main.ProfileId; // only at explicit command time, never per slice
                if (!string.Equals(profileId, MainProfileId, StringComparison.Ordinal) && !string.Equals(profileId, WedgeProfileId, StringComparison.Ordinal))
                    throw new InvalidOperationException("GC mode control requires an allowlisted private QA MainPlayer.");

                stage = "read-before";
                property = typeof(GarbageCollector).GetProperty("GCMode", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingMemberException(typeof(GarbageCollector).FullName, "GCMode");
                if (!property.CanRead || property.GetMethod == null || !property.CanWrite || property.SetMethod == null)
                    throw new InvalidOperationException("GCMode must expose a public static getter and setter.");
                beforeValue = property.GetValue(null) ?? throw new InvalidOperationException("GCMode getter returned null.");
                before = beforeValue.ToString();
                disabledBefore = IL2CPP.il2cpp_gc_is_disabled();

                object requested;
                if (mode == "Restore")
                {
                    if (_savedOriginal == null) throw new InvalidOperationException("No original GCMode was saved in this process.");
                    requested = _savedOriginal;
                }
                else
                {
                    Type enumType = property.PropertyType;
                    string nativeMode = mode == "Incremental" ? "Manual" : mode!;
                    if (!enumType.IsEnum || !Enum.IsDefined(enumType, nativeMode))
                        throw new InvalidOperationException("Requested GCMode value is unsupported: " + nativeMode);
                    requested = Enum.Parse(enumType, nativeMode, false);
                }
                if (mode != "Restore" && _savedOriginal == null)
                {
                    _savedOriginal = beforeValue;
                    _savedOriginalName = before;
                    _savedOriginalDisabled = disabledBefore;
                }

                stage = "setter";
                if (mode == "Incremental")
                {
                    Spt5NativeGcCompatibility.BeginQaIncremental(world, main);
                    incrementalStarted = true;
                }
                else
                {
                    if (!Spt5NativeGcCompatibility.EndQaIncremental("explicit-" + mode))
                        throw new InvalidOperationException("Incremental experiment restoration is pending; requested mode was not applied.");
                    property.SetValue(null, requested);
                }
                stage = "readback";
                object? afterValue = property.GetValue(null);
                after = afterValue?.ToString();
                disabledAfter = IL2CPP.il2cpp_gc_is_disabled();
                bool exactMode = afterValue != null && Equals(afterValue, requested);
                bool manual = string.Equals(requested.ToString(), "Manual", StringComparison.Ordinal);
                // Manual's native disabled flag is an observation, not the Disabled
                // mode contract. A false CollectIncremental result also does not
                // establish lack of support: it can simply mean collection finished.
                bool exactNative = manual || (mode == "Restore"
                    ? disabledAfter.HasValue && _savedOriginalDisabled.HasValue && disabledAfter.Value == _savedOriginalDisabled.Value
                    : disabledAfter.HasValue && disabledAfter.Value == (mode == "Disabled"));
                bool supported = mode != "Incremental" || GarbageCollector.isIncremental;
                bool passed = exactMode && exactNative && supported;
                if (!exactMode) error = "GCMode readback differs from requested value.";
                else if (!exactNative) error = "IL2CPP GC disabled readback differs from requested mode.";
                else if (!supported) error = "Incremental garbage collection support readback is false.";
                if (!passed && incrementalStarted) Spt5NativeGcCompatibility.EndQaIncremental("command-readback-failed");
                return BuildResult(passed, stage, mode, before, after, disabledBefore, disabledAfter, _savedOriginalName, error, _savedOriginalDisabled);
            }
            catch (Exception failure)
            {
                error = failure.ToString();
                if (incrementalStarted) Spt5NativeGcCompatibility.EndQaIncremental("command-error");
                if (property != null)
                {
                    try { after = property.GetValue(null)?.ToString(); }
                    catch { }
                    try { disabledAfter = IL2CPP.il2cpp_gc_is_disabled(); }
                    catch { }
                }
                return BuildResult(false, stage, mode, before, after, disabledBefore, disabledAfter, _savedOriginalName, error, _savedOriginalDisabled);
            }
        }

        private static object BuildResult(bool passed, string stage, string? requested, string? before, string? after, bool? disabledBefore, bool? disabledAfter, string? saved, string? error, bool? savedDisabled)
            => new { passed, stage, modeRequested = requested, beforeGcMode = before, afterGcMode = after,
                il2cppGcDisabledBefore = disabledBefore, il2cppGcDisabledAfter = disabledAfter,
                savedOriginalMode = saved, savedOriginalGcDisabled = savedDisabled, error,
                qaIncremental = Spt5NativeGcCompatibility.IncrementalSnapshot(),
                passedMeaning = "GC mode/support readback only; not performance acceptance", performanceAcceptance = false };
    }
}
