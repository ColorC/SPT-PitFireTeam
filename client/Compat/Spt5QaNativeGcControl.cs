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
            string stage = "read-before";
            try
            {
                property = typeof(GarbageCollector).GetProperty("GCMode", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingMemberException(typeof(GarbageCollector).FullName, "GCMode");
                if (!property.CanRead || property.GetMethod == null || !property.CanWrite || property.SetMethod == null)
                    throw new InvalidOperationException("GCMode must expose a public static getter and setter.");
                beforeValue = property.GetValue(null) ?? throw new InvalidOperationException("GCMode getter returned null.");
                before = beforeValue.ToString();
                disabledBefore = IL2CPP.il2cpp_gc_is_disabled();

                stage = "mode-validation";
                if (mode != "Enabled" && mode != "Disabled" && mode != "Restore")
                    throw new ArgumentException("Mode must be Enabled, Disabled, or Restore.", nameof(mode));
                stage = "private-shadow-guard";
                if (!string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_SHADOW_MODE"), "true", StringComparison.OrdinalIgnoreCase)
                    || !Uri.TryCreate(Environment.GetEnvironmentVariable("PIT_SPT5_BACKEND_URL"), UriKind.Absolute, out Uri? backend)
                    || backend.Scheme != Uri.UriSchemeHttps || !backend.IsLoopback || backend.Port != 6971)
                    throw new InvalidOperationException("GC mode control requires PIT_SPT5_SHADOW_MODE=true and HTTPS loopback backend port 6971.");

                stage = "main-player-guard";
                GameWorld? world = Singleton<GameWorld>.Instance;
                Player? main = world?.MainPlayer;
                if (!Singleton<AbstractGame>.Instantiated || world == null || main == null
                    || !string.Equals(main.ProfileId, MainProfileId, StringComparison.Ordinal))
                    throw new InvalidOperationException("GC mode control requires the allowlisted live MainPlayer during an active raid.");

                object requested;
                if (mode == "Restore")
                {
                    if (_savedOriginal == null) throw new InvalidOperationException("No original GCMode was saved in this process.");
                    requested = _savedOriginal;
                }
                else
                {
                    Type enumType = property.PropertyType;
                    if (!enumType.IsEnum || !Enum.IsDefined(enumType, mode))
                        throw new InvalidOperationException("Requested GCMode value is unsupported: " + mode);
                    requested = Enum.Parse(enumType, mode, false);
                }
                if (mode != "Restore" && _savedOriginal == null)
                {
                    _savedOriginal = beforeValue;
                    _savedOriginalName = before;
                    _savedOriginalDisabled = disabledBefore;
                }

                stage = "setter";
                property.SetValue(null, requested);
                stage = "readback";
                object? afterValue = property.GetValue(null);
                after = afterValue?.ToString();
                disabledAfter = IL2CPP.il2cpp_gc_is_disabled();
                bool exactMode = afterValue != null && Equals(afterValue, requested);
                bool exactNative = mode == "Restore" ? (disabledAfter.HasValue && _savedOriginalDisabled.HasValue && disabledAfter.Value == _savedOriginalDisabled.Value) : (disabledAfter.HasValue && disabledAfter.Value == (mode == "Disabled"));
                bool passed = exactMode && exactNative;
                if (!exactMode) error = "GCMode readback differs from requested value.";
                else if (!exactNative) error = "IL2CPP GC disabled readback differs from requested mode.";
                return BuildResult(passed, stage, mode, before, after, disabledBefore, disabledAfter, _savedOriginalName, error, _savedOriginalDisabled);
            }
            catch (Exception failure)
            {
                error = failure.ToString();
                if (property != null)
                {
                    try { after = property.GetValue(null)?.ToString(); }
                    catch { }
                }
                try { disabledAfter = IL2CPP.il2cpp_gc_is_disabled(); }
                catch { }
                return BuildResult(false, stage, mode, before, after, disabledBefore, disabledAfter, _savedOriginalName, error, _savedOriginalDisabled);
            }
        }

        private static object BuildResult(bool passed, string stage, string? requested, string? before, string? after, bool? disabledBefore, bool? disabledAfter, string? saved, string? error, bool? savedDisabled)
            => new { passed, stage, modeRequested = requested, beforeGcMode = before, afterGcMode = after, il2cppGcDisabledBefore = disabledBefore, il2cppGcDisabledAfter = disabledAfter, savedOriginalMode = saved, savedOriginalGcDisabled = savedDisabled, error };
    }
}
