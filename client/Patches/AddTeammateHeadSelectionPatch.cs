using Arena.UI;
using Comfort.Common;
using EFT;
using EFT.UI;
using pitTeam.Modules;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;

namespace pitTeam.Patches
{
    internal static class AddTeammateNicknameFieldUi
    {
        private static readonly pitTeam.Compat.NativeUiMemberAccessor? StatusLabelAccessor = pitTeam.Compat.NativeUiMemberAccessor.TryForProperty(typeof(NicknameField), "_statusLabel", typeof(TMP_Text), false, false);

        public static void SetStatusLabelText(NicknameField nicknameField, string text)
        {
            TMP_Text statusLabel = StatusLabelAccessor?.GetValue(nicknameField) as TMP_Text;
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
        }
    }

    internal class AddTeammateNicknameFieldEndEditPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(NicknameField), nameof(NicknameField.NicknameSubmitedHandler));
        }

        [PatchPrefix]
        private static bool PatchPrefix(NicknameField __instance, string nickname)
        {
            bool isTeammateCreationFlow = AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController);
            bool isRenameOverlayField = ReferenceEquals(__instance, OtherPlayerProfileScreenPatch.RenameOverlayField);
            if (!isTeammateCreationFlow && !isRenameOverlayField)
            {
                return true;
            }

            __instance.ValidateEnteredString(nickname);
            if (isTeammateCreationFlow)
            {
                AddTeammateCreationFlow.RefreshSubmitButton();
            }

            return false;
        }
    }

    internal class AddTeammateNicknameValueChangedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(HeadSelectionState), nameof(HeadSelectionState.NicknameValueChangeHandler));
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            if (!AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController))
            {
                return;
            }

            AddTeammateCreationFlow.RefreshSubmitButton();
        }
    }

    internal class AddTeammateHeadSelectionOptionsPatch : ModulePatch
    {
        private static object? _probeOptions;
        private static AppearanceSelector? _probeSelector;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(AppearanceSelector), nameof(AppearanceSelector.CollectAvailableCustomization), Type.EmptyTypes);
        }

        [PatchPostfix]
        private static void PatchPostfix(AppearanceSelector __instance)
        {
            if (!AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController)) return;
            var solver = Singleton<EFT.CustomizationSolver>.Instance;
            if (solver == null || __instance == null) return;

            // SPT5 moved the selectors and their typed template lists from HeadSelectionState
            // into AppearanceSelector. Populate them before native Show prepares the controls.
            int originalHeads = __instance._heads?.Count ?? 0;
            int originalVoices = __instance._voices?.Count ?? 0;
            var heads = CollectAllHeads(solver);
            var voices = CollectAllVoices(solver);
            if (heads.Count == 0 || voices.Count == 0) throw new InvalidOperationException("Teammate appearance options are empty.");
            __instance._heads = heads.Select(entry => entry.Value).ToNativeList();
            __instance._voices = voices.Select(entry => entry.Value).ToNativeList();
            __instance._voiceTagBanks?.Clear();
            if (Spt5RuntimeInstallProbe.Enabled)
            {
                _probeSelector = __instance;
                _probeOptions = new { originalHeads, originalVoices, heads = __instance._heads.Count, voices = __instance._voices.Count,
                    headIds = heads.Select(entry => entry.Key.ToString()).ToArray(), voiceIds = voices.Select(entry => entry.Key.ToString()).ToArray() };
            }
        }

        public static object GetProbeSnapshot()
        {
            bool active = AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController)
                && _probeSelector != null && _probeSelector.gameObject.activeInHierarchy;
            return new { available = active && _probeOptions != null, active, frame = UnityEngine.Time.frameCount,
                selectorPointer = active ? _probeSelector!.Pointer.ToString("X") : null,
                nativeHeads = active ? Enumerable.Range(0, _probeSelector!._heads.Count).Select(index => _probeSelector._heads[index].Id.ToString()).ToArray() : Array.Empty<string>(),
                nativeVoices = active ? Enumerable.Range(0, _probeSelector!._voices.Count).Select(index => _probeSelector._voices[index].Id.ToString()).ToArray() : Array.Empty<string>(),
                options = _probeOptions };
        }

        private static List<KeyValuePair<MongoID, EFT.Customization.CustomizationHead>> CollectAllHeads(EFT.CustomizationSolver solver)
        {
            Dictionary<MongoID, EFT.Customization.CustomizationHead> heads = new Dictionary<MongoID, EFT.Customization.CustomizationHead>();
            foreach (EPlayerSide side in new[] { EPlayerSide.Bear, EPlayerSide.Usec, EPlayerSide.Savage })
            {
                foreach (EFT.Customization.CustomizationHead head in solver.GetAvailableHeads(side))
                {
                    if (head != null && !heads.ContainsKey(head.Id))
                    {
                        heads[head.Id] = head;
                    }
                }
            }

            return heads
                .OrderBy(entry => entry.Value?.NameLocalizationKey.Localized(null) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new KeyValuePair<MongoID, EFT.Customization.CustomizationHead>(entry.Key, entry.Value))
                .ToList();
        }

        private static List<KeyValuePair<MongoID, EFT.Customization.CustomizationPlayerVoice>> CollectAllVoices(EFT.CustomizationSolver solver)
        {
            Dictionary<MongoID, EFT.Customization.CustomizationPlayerVoice> voices = new Dictionary<MongoID, EFT.Customization.CustomizationPlayerVoice>();
            foreach (EPlayerSide side in new[] { EPlayerSide.Bear, EPlayerSide.Usec, EPlayerSide.Savage })
            {
                foreach (EFT.Customization.CustomizationPlayerVoice voice in solver.GetAvailableVoices(side))
                {
                    if (voice != null && !voices.ContainsKey(voice.Id))
                    {
                        voices[voice.Id] = voice;
                    }
                }
            }

            return voices
                .OrderBy(entry => entry.Value?.NameLocalizationKey.Localized(null) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new KeyValuePair<MongoID, EFT.Customization.CustomizationPlayerVoice>(entry.Key, entry.Value))
                .ToList();
        }
    }

    internal class AddTeammateFinishPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EftAccountSideSelectionScreen).BaseType, "NextButtonPressedHandler");
        }

        [PatchPrefix]
        private static bool PatchPrefix(object __instance)
        {
            if (!AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController))
            {
                return true;
            }

            AddTeammateCreationFlow.TryCompleteFromCurrentScreen();
            return false;
        }
    }

    internal class AddTeammateNicknameFieldInitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(NicknameField), nameof(NicknameField.Init));
        }

        [PatchPostfix]
        private static void PatchPostfix(NicknameField __instance)
        {
            if (!AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController))
            {
                return;
            }

            AddTeammateNicknameFieldUi.SetStatusLabelText(
                __instance,
                AddTeammateCreationFlow.GetLocalizedSocialUi("EnterNickname"));
        }
    }

    internal class AddTeammateNicknameFieldStatusPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(NicknameField), nameof(NicknameField.ShowNicknameError));
        }

        [PatchPostfix]
        private static void PatchPostfix(NicknameField __instance, ENicknameError error, bool isFromBackend = false)
        {
            bool isTeammateCreationFlow = AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController);
            bool isRenameOverlayField = ReferenceEquals(__instance, OtherPlayerProfileScreenPatch.RenameOverlayField);
            if ((!isTeammateCreationFlow && !isRenameOverlayField) || isFromBackend)
            {
                return;
            }

            if (error == ENicknameError.TooShort)
            {
                AddTeammateNicknameFieldUi.SetStatusLabelText(
                    __instance,
                    AddTeammateCreationFlow.GetLocalizedSocialUi("NicknameTooShort"));
                return;
            }

            if (error == ENicknameError.ValidNickname)
            {
                AddTeammateNicknameFieldUi.SetStatusLabelText(
                    __instance,
                    AddTeammateCreationFlow.GetLocalizedSocialUi("EnterNickname"));
            }
        }
    }
}
