using System;
using System.Linq;
using EFT;
using EFT.Counters;
using EFT.Communications;
using EFT.UI;
using UnityEngine;

namespace pitTeam.Patches
{
    internal partial class OtherPlayerProfileScreenPatch
    {
        internal static object ShortStatsProbe { get; private set; }
        private static readonly System.Collections.Generic.Queue<object> LocalizedTemplateProbe = new();
        private sealed class ShortLabelStyle
        {
            internal TMPro.TMP_Text Label;
            internal float FontSize, Minimum, Maximum;
            internal bool AutoSizing, WordWrapping;
        }
        private static readonly System.Collections.Generic.List<ShortLabelStyle> ShortLabelStyles = new();

        private static void RestoreShortStatsLabelStyles()
        {
            foreach (var style in ShortLabelStyles)
            {
                if (style.Label == null) continue;
                style.Label.enableAutoSizing = style.AutoSizing;
                style.Label.enableWordWrapping = style.WordWrapping;
                style.Label.fontSizeMin = style.Minimum;
                style.Label.fontSizeMax = style.Maximum;
                style.Label.fontSize = style.FontSize;
            }
            ShortLabelStyles.Clear();
        }

        private static void PrepareLocalizedTemplates(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<LocalizedText>(true))
                PrepareLocalizedTemplate(text);
        }

        internal static bool HasTeammateProfileTextContext => PendingBackOverrideAction != null || Modules.SquadSideSelectionFlow.SquadModeActive || ViewedProfile != null;

        internal static void PrepareLocalizedTemplate(LocalizedText text)
        {
                if (text == null || !string.IsNullOrEmpty(text._formatTemplate)) return;
                string before = text._formatTemplate;
                string keyBefore = text.LocalizationKey;
                try
                {
                    // Native OnEnable first discovers its TMP labels and derives an
                    // absent key from the serialized label, then initializes locale.
                    // Do not invoke OnEnable itself: that would subscribe twice.
                    text.GetTextFromObject();
                    text.UpdateLocale();
                    if (pitTeam.Compat.Spt5RuntimeInstallProbe.Enabled)
                    {
                        LocalizedTemplateProbe.Enqueue(new { name = text.name, keyBefore, key = text.LocalizationKey,
                            labelCount = text._labels?.Count, labels = text._labels?.ToArray().Select(label => label?.text).ToArray(),
                            active = text.gameObject.activeInHierarchy, before, after = text._formatTemplate });
                        while (LocalizedTemplateProbe.Count > 128) LocalizedTemplateProbe.Dequeue();
                    }
                }
                catch (Exception ex)
                {
                    pitFireTeam.Log.LogError($"[UI] Failed to initialize localized profile template '{text.name}' key='{text.LocalizationKey}': {ex}");
                }
        }

        private static void RefreshTeammateShortStats(OtherPlayerProfile profile, InventoryPlayerModelWithStatsWindow window)
        {
            try
            {
                // The native short panel sums faction kill counters; the follower
                // settlement contract stores an accurate total without faction tags.
                // Preserve the stored total and every other native localized statistic.
                var stats = profile.Side == EPlayerSide.Savage ? profile.ScavStats?.Eft : profile.PmcStats?.Eft;
                var counters = stats?.OverallCounters;
                var panel = window?._shortStatsPanel;
                if (counters == null || panel?._kills == null || panel._killsDeathRate == null)
                    throw new InvalidOperationException("Teammate short statistics are missing their native counters or labels.");

                long kills = Math.Max(0L, counters.GetLong(PredefinedCounters.Kills));
                var deathTag = Il2CppSystem.Enum.ToObject(Il2CppInterop.Runtime.Il2CppType.Of<CounterTag>(), (int)CounterTag.Deaths);
                long deaths = Math.Max(0L, counters.GetLong(new[] { (Il2CppSystem.Object)deathTag }));
                float ratio = (float)Math.Round((double)kills / Math.Max(1L, deaths), 2);
                panel._kills.SetFormatValues(new[] { (Il2CppSystem.Object)kills });
                panel._killsDeathRate.SetFormatValues(new[] { (Il2CppSystem.Object)ratio });
                // Two-digit totals in Chinese exceeded the stock short-column
                // width and wrapped over the following statistic.
                foreach (var label in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    ShortLabelStyles.Add(new ShortLabelStyle { Label = label, FontSize = label.fontSize,
                        Minimum = label.fontSizeMin, Maximum = label.fontSizeMax,
                        AutoSizing = label.enableAutoSizing, WordWrapping = label.enableWordWrapping });
                    label.fontSizeMax = label.fontSize;
                    label.fontSizeMin = Math.Min(label.fontSize, 13f);
                    label.enableAutoSizing = true;
                    label.enableWordWrapping = false;
                }
                ShortStatsProbe = new { accountId = profile.AccountId, side = profile.Side.ToString(), kills, deaths, ratio,
                    counterPointer = counters.Pointer.ToString("X"), renderedKills = panel._kills.FormattedText,
                    renderedKd = panel._killsDeathRate.FormattedText };
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError($"[UI] Failed to display exact short statistics for teammate '{profile?.AccountId}'.");
                Modules.Logger.LogError(ex);
                EFT.Communications.NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileControlsUnavailable"), ENotificationDurationType.Default);
            }
        }
    }

    internal static class TeammateLifetimeFormatting
    {
        [ThreadStatic] internal static LocalizedText Current;
        internal static bool Ready { get; private set; }
        private static int ClearCount;

        internal static bool VerifyScope()
        {
            var info = HarmonyLib.Harmony.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(OverallStatsPanel), nameof(OverallStatsPanel.SetOverallLifetime)));
            var prefixes = info?.Prefixes.Where(p => p.PatchMethod.DeclaringType == typeof(TeammateOverallLifetimeContextPatch)).ToArray();
            var finalizers = info?.Finalizers.Where(p => p.PatchMethod.DeclaringType == typeof(TeammateOverallLifetimeContextPatch)).ToArray();
            Ready = prefixes?.Length == 1 && finalizers?.Length == 1 && prefixes[0].owner == finalizers[0].owner;
            if (!Ready) Current = null;
            return Ready;
        }

        internal static void RecordClear() { ClearCount++; }
        internal static object Snapshot() => new { ready = Ready, active = Current != null, clearCount = ClearCount };
    }

    internal class TeammateOverallLifetimeContextPatch : SPTushonka.Reflection.Patching.ModulePatch
    {
        protected override System.Reflection.MethodBase GetTargetMethod() => HarmonyLib.AccessTools.Method(typeof(OverallStatsPanel), nameof(OverallStatsPanel.SetOverallLifetime));

        [SPTushonka.Reflection.Patching.PatchPrefix]
        private static void Prefix(OverallStatsPanel __instance, out LocalizedText __state)
        {
            __state = TeammateLifetimeFormatting.Current;
            if (TeammateLifetimeFormatting.Ready)
                TeammateLifetimeFormatting.Current = OtherPlayerProfileScreenPatch.HasTeammateProfileTextContext ? __instance._overallLifetimeLabel : null;
        }

        [HarmonyLib.HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, LocalizedText __state)
        {
            TeammateLifetimeFormatting.Current = __state;
            return __exception;
        }
    }

    internal class TeammateLifetimeClearFormatPatch : SPTushonka.Reflection.Patching.ModulePatch
    {
        protected override System.Reflection.MethodBase GetTargetMethod() => HarmonyLib.AccessTools.Method(typeof(LocalizedText), nameof(LocalizedText.SetFormatValues),
            new[] { typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>) });

        [SPTushonka.Reflection.Patching.PatchPrefix]
        private static bool Prefix(LocalizedText __instance, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object> values)
        {
            if (!TeammateLifetimeFormatting.Ready || TeammateLifetimeFormatting.Current == null
                || !pitTeam.Compat.Il2CppBoundary.SameNativeObject(__instance, TeammateLifetimeFormatting.Current)
                || values != null || !string.IsNullOrEmpty(__instance.LocalizationKey) || !string.IsNullOrEmpty(__instance._formatTemplate)) return true;
            // Native SetOverallLifetime explicitly empties the key/template, then
            // calls SetFormatValues(null) before assigning its age-specific key.
            // Preserve that clear assignment; there is no template to format yet.
            __instance._formatValues = null;
            TeammateLifetimeFormatting.RecordClear();
            return false;
        }
    }
}
