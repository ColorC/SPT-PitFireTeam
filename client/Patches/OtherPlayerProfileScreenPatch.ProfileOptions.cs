using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Newtonsoft.Json;
using SPTushonka.Common.Http;
using SPTushonka.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using dropDownItem = EFT.Customization.CustomizationSuite;
using OtherProfileResult = EFT.OtherPlayerProfileDescriptor;
using ResultProfile = EFT.OtherPlayerProfile;

namespace pitTeam.Patches
{
    internal partial class OtherPlayerProfileScreenPatch
    {
        private static CustomTextMeshProUGUI ScenarioGuardUniformLabel;

        private static bool IsScenarioGuardUniform(string body, string feet)
        {
            return Modules.ScenarioStaminaSupport.IsConfiguredProfile(ActiveProfileSession?.Profile?.ProfileId)
                && ViewedProfile?.AccountId is ("1047489" or "1709376" or "1246804" or "1102188")
                && ((body == "688de6ea74514d35fd003554" && feet == "688de71374514d35fd003556")
                    || (body == "688dec55152c0d07670564af" && feet == "688dec1429e9479b850276b9"));
        }

        private static void ClearScenarioGuardUniformLabel()
        {
            if (ScenarioGuardUniformLabel != null)
            {
                GameObject.Destroy(ScenarioGuardUniformLabel.gameObject);
                ScenarioGuardUniformLabel = null;
            }
        }

        private static string GetSocialUiText(string key)
        {
            return pitFireTeam.GetSocialUiText(key);
        }

        private static void EnsureBodySuccess(string responseJson)
        {
            DeserializeBodySuccess<object>(responseJson);
        }

        private static FriendlyTeammateBodyResponse<T> DeserializeBodySuccess<T>(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return null;
            }

            FriendlyTeammateBodyResponse<T> body = pitTeam.Compat.SptJsonBoundary.Deserialize<FriendlyTeammateBodyResponse<T>>(responseJson);

            if (body != null && body.err != 0)
            {
                throw new InvalidOperationException(body.errmsg ?? "Unknown teammate backend error");
            }

            return body;
        }

        private static FriendlyTeammateProfileOptions TryLoadProfileOptions(string accountId)
        {
            try
            {
                string responseJson = RequestHandler.PostJson(OptionsRoute, SerializeBody(new { aid = accountId }));
                FriendlyTeammateBodyResponse<FriendlyTeammateProfileOptions> body =
                    pitTeam.Compat.SptJsonBoundary.Deserialize<FriendlyTeammateBodyResponse<FriendlyTeammateProfileOptions>>(responseJson);

                if (body != null && body.err != 0) throw new InvalidOperationException(body.errmsg);

                if (body?.data != null)
                {
                    return body.data;
                }

                return pitTeam.Compat.SptJsonBoundary.Deserialize<FriendlyTeammateProfileOptions>(responseJson);
            }
            catch (Exception ex)
            {
                pitFireTeam.Log.LogError($"[UI] Failed to load teammate profile options for '{accountId}'.");
                pitFireTeam.Log.LogError(ex);
                return null;
            }
        }

        private static string SerializeBody(object body)
        {
            return pitTeam.Compat.SptJsonBoundary.Serialize(body);
        }
        private static void HideProfileActions(OtherPlayerProfileScreen screen)
        {
            ReportPanel reportPanel = ReportPanelAccessor.GetValue(screen) as ReportPanel;
            if (reportPanel != null)
            {
                reportPanel.Close();
                reportPanel.gameObject.SetActive(false);
            }
        }

        private static void ClearProfileRightSideContent(OtherPlayerProfileScreen screen)
        {
            HideProfileRightSideRoot(screen, screen?.transform.Find("Overall")?.gameObject);
            Transform rightSide = screen?.transform.Find("RightSide")
                ?? FindChildRecursive(screen?.transform, "RightSide");
            if (rightSide != null)
            {
                foreach (Transform child in pitTeam.Compat.Il2CppBoundary.EnumerateNativeChildren(rightSide))
                {
                    HideProfileRightSideRoot(screen, child.gameObject);
                }
            }
            HideProfileRightSideRoot(screen, OverallStatsPanelAccessor.GetValue(screen) as Component);
            HideProfileRightSideRoot(screen, AchievementsProgressBlockAccessor.GetValue(screen) as GameObject);
            HideProfileRightSideRoot(screen, AchievementsBlockPlaceholderAccessor.GetValue(screen) as GameObject);
            HideProfileRightSideRoot(screen, WeaponsBlockPlaceholderAccessor.GetValue(screen) as GameObject);
            HideProfileRightSideRoot(screen, NonWeaponItemsBlockPlaceholderAccessor.GetValue(screen) as GameObject);
            HideProfileRightSideRoot(screen, WeaponsGridLayoutGroupAccessor.GetValue(screen) as Component);
            HideProfileRightSideRoot(screen, NonWeaponItemsGridLayoutGroupAccessor.GetValue(screen) as Component);
        }

        private static void HideProfileRightSideRoot(OtherPlayerProfileScreen screen, Component component)
        {
            HideProfileRightSideRoot(screen, component?.gameObject);
        }

        private static void HideProfileRightSideRoot(OtherPlayerProfileScreen screen, GameObject target)
        {
            if (screen == null || target == null)
            {
                return;
            }

            GameObject root = ResolveProfileSectionRoot(screen.transform, target.transform);
            if (root == null || root.Pointer == IntPtr.Zero) return;
            long rootPointer = root.Pointer.ToInt64();
            if (HiddenRightSideRoots.TryGetValue(rootPointer, out var existing))
            {
                if (existing.root != null && existing.root.Pointer == root.Pointer) return;
                HiddenRightSideRoots.Remove(rootPointer);
            }
            HiddenRightSideRoots[rootPointer] = new HiddenRightSideRootState { root = root, active = root.activeSelf };
            root.SetActive(false);
        }

        private static GameObject ResolveProfileSectionRoot(Transform screenRoot, Transform target)
        {
            if (screenRoot == null || target == null)
            {
                return null;
            }

            Transform rightSideRoot = screenRoot.Find("RightSide")
                ?? FindChildRecursive(screenRoot, "RightSide");
            if (rightSideRoot != null && target.IsChildOf(rightSideRoot))
            {
                Transform currentRightSide = target;
                while (currentRightSide.parent != null && currentRightSide.parent != rightSideRoot)
                {
                    currentRightSide = currentRightSide.parent;
                }

                return currentRightSide == rightSideRoot ? null : currentRightSide.gameObject;
            }

            Transform current = target;
            while (current.parent != null && current.parent != screenRoot)
            {
                current = current.parent;
            }

            return current == screenRoot ? null : current.gameObject;
        }

        private static void ConfigureLoadoutPanel(InventoryClothingSelectionPanel panel, InventoryClothingSelectionPanel sourcePanel)
        {
            InventoryCustomizationSelector upperDropdown = UpperSelectorAccessor.GetValue(panel) as InventoryCustomizationSelector;
            InventoryCustomizationSelector lowerDropdown = LowerSelectorAccessor.GetValue(panel) as InventoryCustomizationSelector;

            if (upperDropdown != null && lowerDropdown != null)
            {
                ReplaceDropdownIcon(panel.transform, "Upper/Icon", GearIconPath);
                ReplaceDropdownIcon(panel.transform, "Lower/Icon", BrainIconPath);
                upperDropdown.gameObject.SetActive(true);
                lowerDropdown.gameObject.SetActive(true);
            }
        }

        private static void ApplyLoadoutPanelLayout(InventoryClothingSelectionPanel panel, InventoryClothingSelectionPanel sourcePanel, bool useLowerSource = false)
        {
            ApplyDropdownLayout(
                UpperSelectorAccessor.GetValue(panel) as InventoryCustomizationSelector,
                UpperSelectorAccessor.GetValue(sourcePanel) as InventoryCustomizationSelector);
            ApplyDropdownLayout(
                LowerSelectorAccessor.GetValue(panel) as InventoryCustomizationSelector,
                LowerSelectorAccessor.GetValue(sourcePanel) as InventoryCustomizationSelector);

            ApplyIconLayout(panel.transform, "Upper/Icon", sourcePanel.transform, "Upper/Icon");
            ApplyIconLayout(panel.transform, "Lower/Icon", sourcePanel.transform, "Lower/Icon");
        }

        private static void ApplyDropdownLayout(InventoryCustomizationSelector targetDropdown, InventoryCustomizationSelector sourceDropdown)
        {
            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(targetDropdown?.transform) is not RectTransform targetRect || pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(sourceDropdown?.transform) is not RectTransform sourceRect)
            {
                return;
            }

            targetRect.anchorMin = sourceRect.anchorMin;
            targetRect.anchorMax = sourceRect.anchorMax;
            targetRect.pivot = sourceRect.pivot;
            targetRect.anchoredPosition = sourceRect.anchoredPosition;
            targetRect.sizeDelta = sourceRect.sizeDelta;
            targetRect.offsetMin = sourceRect.offsetMin;
            targetRect.offsetMax = sourceRect.offsetMax;
            targetRect.localScale = sourceRect.localScale;
        }

        private static void ApplyIconLayout(Transform targetParent, string targetPath, Transform sourceParent, string sourcePath)
        {
            RectTransform targetRect = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(targetParent?.Find(targetPath));
            RectTransform sourceRect = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(sourceParent?.Find(sourcePath));
            if (targetRect == null || sourceRect == null)
            {
                return;
            }

            targetRect.anchorMin = sourceRect.anchorMin;
            targetRect.anchorMax = sourceRect.anchorMax;
            targetRect.pivot = sourceRect.pivot;
            targetRect.anchoredPosition = sourceRect.anchoredPosition;
            targetRect.sizeDelta = sourceRect.sizeDelta;
            targetRect.offsetMin = sourceRect.offsetMin;
            targetRect.offsetMax = sourceRect.offsetMax;
            targetRect.localScale = sourceRect.localScale;
        }

        private static void ReplaceDropdownIcon(Transform parent, string childPath, string filePath)
        {
            Transform iconTransform = parent.Find(childPath);
            if (iconTransform == null)
            {
                return;
            }

            Image image = iconTransform.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                image.enabled = false;
                return;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(fileData))
            {
                image.enabled = false;
                UnityEngine.Object.Destroy(texture);
                return;
            }

            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 200f);
            image.enabled = true;
            image.preserveAspect = true;
        }

        private static void HideDropdownIcon(Transform parent, string childPath)
        {
            Transform iconTransform = parent.Find(childPath);
            if (iconTransform == null)
            {
                return;
            }

            Image image = iconTransform.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
            }
        }

        private static void DisplayClothingOptions(
            string profileVersion,
            EFT.PlayerVisualRepresentation playerVisualRepresentation,
            InventoryPlayerModelWithStatsWindow window,
            InventoryController inventoryController,
            InventoryClothingSelectionPanel panel,
            FriendlyTeammateProfileOptions options
        )
        {
            EFT.CustomizationSolver solver = Singleton<EFT.CustomizationSolver>.Instance;
            MongoID selectedBody = playerVisualRepresentation.Customization[EBodyModelPart.Body];
            MongoID selectedFeet = playerVisualRepresentation.Customization[EBodyModelPart.Feet];
            ClearScenarioGuardUniformLabel();
            if (IsScenarioGuardUniform(selectedBody.ToString(), selectedFeet.ToString()))
            {
                // These installed NPC body parts have no retail Upper/LowerBodySuit.
                // Preserve their actual models without passing null selections to native Init.
                panel.gameObject.SetActive(false);
                ScenarioGuardUniformLabel = CreateOverlayText(
                    "pitFireTeam_ScenarioGuardUniform", panel.transform.parent,
                    Vector2.zero, Vector2.zero, TextAlignmentOptions.Center,
                    GetSocialUiText("ScenarioGuardUniform"), 18f, Color.white);
                var source = panel.GetComponent<RectTransform>();
                var target = ScenarioGuardUniformLabel.GetComponent<RectTransform>();
                target.anchorMin = source.anchorMin;
                target.anchorMax = source.anchorMax;
                target.pivot = source.pivot;
                target.anchoredPosition = source.anchoredPosition;
                target.sizeDelta = source.sizeDelta;
                ScenarioGuardUniformLabel.raycastTarget = false;
                return;
            }
            HashSet<string> ownedBodyIds = new HashSet<string>(
                options.OwnedBodyCustomizationIds ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> ownedFeetIds = new HashSet<string>(
                options.OwnedFeetCustomizationIds ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            ownedBodyIds.Add(selectedBody.ToString());
            ownedFeetIds.Add(selectedFeet.ToString());

            List<dropDownItem> followerOwnedSuites = new List<dropDownItem>();
            foreach (dropDownItem suite in solver._customizationSuiteTemplates.Values)
            {
                if (suite != null && ((pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<EFT.Customization.UpperBodySuit>(suite) != null && ownedBodyIds.Contains(suite.MainBodyPartItem.ToString()))
                    || (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<EFT.Customization.LowerBodySuit>(suite) != null && ownedFeetIds.Contains(suite.MainBodyPartItem.ToString()))))
                {
                    followerOwnedSuites.Add(suite);
                }
            }

            List<dropDownItem> allSuites = new List<dropDownItem>(followerOwnedSuites);
            foreach (EPlayerSide side in new[] { EPlayerSide.Bear, EPlayerSide.Usec, EPlayerSide.Savage })
            {
                foreach (dropDownItem suite in solver.GetAvailableSuites(side, profileVersion))
                {
                    allSuites.Add(suite);
                }
            }
            IEnumerable<dropDownItem> availableSuites = allSuites
                .Where(suite => suite != null)
                .GroupBy(suite => suite.Id.ToString(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());

            List<dropDownItem> upper = new List<dropDownItem>();
            List<dropDownItem> lower = new List<dropDownItem>();
            dropDownItem currentUpper = null;
            dropDownItem currentLower = null;

            foreach (dropDownItem suite in availableSuites)
            {
                var upperSuite = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<EFT.Customization.UpperBodySuit>(suite);
                var lowerSuite = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<EFT.Customization.LowerBodySuit>(suite);
                if (upperSuite != null)
                {
                    upper.Add(upperSuite);
                    if (selectedBody == upperSuite.MainBodyPartItem) currentUpper = upperSuite;
                }
                else if (lowerSuite != null)
                {
                    lower.Add(lowerSuite);
                    if (selectedFeet == lowerSuite.MainBodyPartItem) currentLower = lowerSuite;
                }
            }

            // Native Init dereferences the selected suit asynchronously. Do not pass a null
            // selection (or silently replace the teammate's saved clothes with another suit).
            if (currentUpper == null || currentLower == null || upper.Count == 0 || lower.Count == 0)
            {
                panel.gameObject.SetActive(false);
                pitFireTeam.Log.LogError($"[UI] Teammate clothes unavailable: body={selectedBody}, feet={selectedFeet}, upper={upper.Count}, lower={lower.Count}, selectedUpper={currentUpper != null}, selectedLower={currentLower != null}.");
                EFT.Communications.NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileClothingUnavailable"), ENotificationDurationType.Default);
                return;
            }

            Il2CppSystem.Collections.Generic.List<dropDownItem> upperSuitesIl2Cpp = new Il2CppSystem.Collections.Generic.List<dropDownItem>();
            Il2CppSystem.Collections.Generic.List<dropDownItem> lowerSuitesIl2Cpp = new Il2CppSystem.Collections.Generic.List<dropDownItem>();
            foreach (dropDownItem suite in upper) upperSuitesIl2Cpp.Add(suite);
            foreach (dropDownItem suite in lower) lowerSuitesIl2Cpp.Add(suite);
            panel.gameObject.SetActive(true);
            panel.Show(upperSuitesIl2Cpp, currentUpper, lowerSuitesIl2Cpp, currentLower, false, (System.Action<dropDownItem>)(suite =>
            {
                suite.SetClothingsToProfile(playerVisualRepresentation.Customization);
                if (window._playerModelView.gameObject.activeSelf)
                {
                    window._playerModelView.Close();
                }
                window.ShowPreview(playerVisualRepresentation, inventoryController);
                PlayerModelWithStatsWindow_OnCustomizationChanged(suite);
            }));
        }

        private static void DisplayLoadoutOptions(
            OtherPlayerProfileScreen screen,
            ResultProfile profile,
            InventoryController inventoryController,
            EFT.IEftSession session,
            RectTransform actionContainer,
            InventoryPlayerModelWithStatsWindow window,
            FriendlyTeammateProfileOptions options)
        {
            if (screen == null || actionContainer == null)
            {
                return;
            }

            CreateTeammateProfileBackButton(screen, actionContainer);
            CustomDropdownIds.Clear();
            if (profile == null || options == null) return;

            FriendlyTeammateLoadoutOption currentLoadout = null;
            FriendlyTeammateLoadoutOption defaultLoadout = null;
            foreach (FriendlyTeammateLoadoutOption option in options.Loadouts ?? new List<FriendlyTeammateLoadoutOption>())
            {
                if (option == null || string.IsNullOrWhiteSpace(option.Id))
                {
                    continue;
                }

                if (string.Equals(option.Id, DefaultLoadoutId, StringComparison.OrdinalIgnoreCase))
                {
                    defaultLoadout = option;
                }

                if (string.Equals(option.Id, options.CurrentLoadoutId, StringComparison.OrdinalIgnoreCase))
                {
                    currentLoadout = option;
                }
            }

            if (pitFireTeam.IsFollowerLoadoutRealTransferMode())
            {
                currentLoadout = defaultLoadout ?? currentLoadout;
            }

            currentLoadout ??= options.Loadouts?.FirstOrDefault(option => option != null && !string.IsNullOrWhiteSpace(option.Id));
            if (currentLoadout == null)
            {
                pitFireTeam.Log.LogWarning("[UI] Profile options skipped: no selectable loadout was returned.");
                return;
            }

            ActiveTeammateLoadoutId = currentLoadout.Id;
            ActiveTeammateLoadoutName = currentLoadout.Name;

            string currentTactic = options.CurrentTactic?.Trim() ?? string.Empty;
            FriendlyTeammateTacticOption selectedTactic = (options.Tactics ?? new List<FriendlyTeammateTacticOption>())
                .FirstOrDefault(option =>
                    option != null &&
                    !string.IsNullOrWhiteSpace(option.Id) &&
                    !IsBetaHiddenTactic(option.Id) &&
                    string.Equals(option.Id, currentTactic, StringComparison.OrdinalIgnoreCase));
            selectedTactic ??= (options.Tactics ?? new List<FriendlyTeammateTacticOption>())
                .FirstOrDefault(option => option != null && !string.IsNullOrWhiteSpace(option.Id) && !IsBetaHiddenTactic(option.Id));
            ActiveProfileTactic = selectedTactic?.Id ?? "Rifleman";

            LoadoutTacticButton = CreateProfileActionButton(
                screen,
                actionContainer,
                "pitFireTeam_LoadoutTacticOptionsButton",
                GetProfileOptionsButtonCaption(),
                1,
                () => ShowProfileOptionsOverlay(screen, profile, inventoryController, session, window, options));
            ConfigureCompactProfileActionButton(EditLoadoutButton, 0);
            ConfigureCompactProfileActionButton(LoadoutTacticButton, 1);
        }

        private static void CreateTeammateProfileBackButton(OtherPlayerProfileScreen screen, RectTransform actionContainer)
        {
            if (screen == null || actionContainer == null || ActiveBackOverrideAction == null) return;
            Action expectedReturn = ActiveBackOverrideAction;
            DefaultUIButton button = CreateProfileActionButton(screen, actionContainer, "pitFireTeam_TeammateProfileBackButton", GetSocialUiText("SquadControlBack"), 0, () =>
            {
                if (screen == null || expectedReturn == null || !ReferenceEquals(ActiveBackOverrideAction, expectedReturn)) return;
                screen.TranslateCommand(EFT.InputSystem.ECommand.Escape);
            });
            RectTransform rect = button == null ? null : pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(button.transform);
            if (rect == null) return;
            HideProfileRightSideRoot(screen, BackButtonField?.GetValue(screen) as Component);
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(180f, 44f); rect.anchoredPosition = new Vector2(12f, -8f); rect.localScale = Vector3.one;
            ApplyProfileButtonSurface(button);
        }


        private static float ProfileActionsHeight => pitFireTeam.IsFollowerLoadoutRealTransferMode() ? 162f : 112f;

        private static void ConfigureCompactProfileActionButton(DefaultUIButton button, int slot)
        {
            if (button == null || slot < 0 || slot > 4)
            {
                return;
            }

            RectTransform host = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(button.transform.parent);
            if (host != null)
            {
                host.anchorMin = new Vector2(0f, 1f);
                host.anchorMax = new Vector2(1f, 1f);
                host.pivot = new Vector2(0.5f, 1f);
                host.anchoredPosition = Vector2.zero;
                host.sizeDelta = new Vector2(0f, ProfileActionsHeight);
                host.localScale = Vector3.one;
                LayoutElement hostLayout = host.GetComponent<LayoutElement>() ?? host.gameObject.AddComponent<LayoutElement>();
                hostLayout.minHeight = ProfileActionsHeight;
                hostLayout.preferredHeight = ProfileActionsHeight;
                hostLayout.flexibleHeight = 0f;
            }

            RectTransform rect = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(button.transform);
            if (rect == null)
            {
                return;
            }

            int column = slot % 2;
            int row = slot / 2;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(220f, 44f);
            rect.anchoredPosition = new Vector2(column == 0 ? -240f : -12f, -(8f + row * 50f));
            rect.localScale = Vector3.one;
            ApplyProfileButtonSurface(button);
        }

        private static void ApplyProfileButtonSurface(DefaultUIButton button)
        {
            if (button == null)
            {
                return;
            }

            Image background = button.GetComponent<Image>();
            if (background == null)
            {
                background = button.gameObject.AddComponent<Image>();
            }

            background.enabled = true;
            background.color = new Color(0.10f, 0.16f, 0.22f, 0.98f);
            background.raycastTarget = true;
        }
        private static string GetProfileOptionsButtonCaption()
        {
            string loadoutName = ActiveTeammateLoadoutName;
            string tacticName = GetTacticDisplayName(ActiveProfileTactic ?? "Rifleman");
            if (string.IsNullOrWhiteSpace(loadoutName))
            {
                loadoutName = DefaultLoadoutName;
            }

            return loadoutName + " / " + tacticName;
        }

        private static void RefreshProfileOptionsButtonCaption()
        {
            if (LoadoutTacticButton != null)
            {
                LoadoutTacticButton.SetRawText(GetProfileOptionsButtonCaption(), 16);
            }
        }

        private static void ShowProfileOptionsOverlay(
            OtherPlayerProfileScreen screen,
            ResultProfile profile,
            InventoryController inventoryController,
            EFT.IEftSession session,
            InventoryPlayerModelWithStatsWindow window,
            FriendlyTeammateProfileOptions options)
        {
            CloseProfileOptionsOverlay();
            if (screen == null || profile == null || options == null)
            {
                return;
            }

            DefaultUIButton buttonTemplate = BackButtonField?.GetValue(screen) as DefaultUIButton;
            if (buttonTemplate == null)
            {
                pitFireTeam.Log.LogWarning("[UI] Loadout/tactic options unavailable: stock button template was not found.");
                return;
            }

            GameObject root = new GameObject(
                "pitFireTeam_ProfileOptionsOverlay",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Image>());
            ProfileOptionsOverlayRoot = root;
            root.transform.SetParent(screen.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.SetAsLastSibling();
            Image backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.55f);
            backdrop.raycastTarget = true;

            GameObject panelObject = new GameObject(
                "pitFireTeam_ProfileOptionsPanel",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Image>());
            panelObject.transform.SetParent(root.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 560f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.localScale = Vector3.one;
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.98f);
            panelImage.raycastTarget = true;

            CreateProfileOptionsTitle(panelObject.transform);

            GameObject scrollObject = new GameObject(
                "pitFireTeam_ProfileOptionsScroll",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<ScrollRect>());
            scrollObject.transform.SetParent(panelObject.transform, false);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(24f, 68f);
            scrollRectTransform.offsetMax = new Vector2(-24f, -62f);

            GameObject viewportObject = new GameObject(
                "pitFireTeam_ProfileOptionsViewport",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Image>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Mask>());
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject("pitFireTeam_ProfileOptionsContent", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(), Il2CppInterop.Runtime.Il2CppType.Of<VerticalLayoutGroup>(), Il2CppInterop.Runtime.Il2CppType.Of<ContentSizeFitter>());
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layoutGroup = contentObject.GetComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.spacing = 6f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            ContentSizeFitter contentSizeFitter = contentObject.GetComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 35f;
            if (!pitFireTeam.IsFollowerLoadoutRealTransferMode())
                AddProfileOptionsHeader(contentObject.transform, GetSocialUiText("ProfileLoadoutsHeader"));
            foreach (FriendlyTeammateLoadoutOption option in pitFireTeam.IsFollowerLoadoutRealTransferMode()
                ? Enumerable.Empty<FriendlyTeammateLoadoutOption>()
                : options.Loadouts ?? new List<FriendlyTeammateLoadoutOption>())
            {
                if (option == null || string.IsNullOrWhiteSpace(option.Id))
                {
                    continue;
                }

                string loadoutId = option.Id;
                string loadoutName = option.Name ?? option.Id;
                AddProfileOptionsRow(buttonTemplate, contentRect, loadoutName, () =>
                {
                    try
                    {
                        string responseJson = RequestHandler.PostJson(LoadoutRoute, SerializeBody(new FriendlyTeammateLoadoutRequest
                        {
                            aid = profile.AccountId,
                            loadoutId = loadoutId
                        }));
                        EnsureBodySuccess(responseJson);
                        ActiveTeammateLoadoutId = loadoutId;
                        ActiveTeammateLoadoutName = loadoutName;
                        CloseProfileOptionsOverlay();
                        RefreshProfileOptionsButtonCaption();
                        MarkSquadRosterDirty(profile.AccountId);
                        RefreshPlayerVisualization(profile, inventoryController, session, window);
                    }
                    catch (Exception ex)
                    {
                        Modules.Logger.LogError("[UI] Failed to persist teammate loadout change.");
                        Modules.Logger.LogError(ex);
                        ShowProfileSaveFailure();
                    }
                });
            }

            AddProfileOptionsHeader(contentObject.transform, GetSocialUiText("ProfileTacticsHeader"));
            IEnumerable<FriendlyTeammateTacticOption> tactics = options.Tactics != null && options.Tactics.Count > 0
                ? options.Tactics
                : new[]
                {
                    new FriendlyTeammateTacticOption { Id = "Rifleman", Name = "Rifleman" },
                    new FriendlyTeammateTacticOption { Id = "Marksman", Name = "Marksman" },
                };

            foreach (FriendlyTeammateTacticOption tactic in tactics)
            {
                if (string.IsNullOrWhiteSpace(tactic?.Id) || IsBetaHiddenTactic(tactic.Id))
                {
                    continue;
                }

                string tacticValue = tactic.Id;
                string tacticName = GetTacticDisplayName(tacticValue);
                AddProfileOptionsRow(buttonTemplate, contentRect, tacticName, () =>
                {
                    try
                    {
                        string responseJson = RequestHandler.PostJson(TacticRoute, SerializeBody(new FriendlyTeammateTacticRequest
                        {
                            aid = profile.AccountId,
                            tactic = tacticValue
                        }));
                        EnsureBodySuccess(responseJson);
                        ActiveProfileTactic = tacticValue;
                        SetProficiencyAggressionForTactic(IsMarksmanTactic(tacticValue));
                        CloseProfileOptionsOverlay();
                        RefreshProfileOptionsButtonCaption();
                        MarkSquadRosterDirty(profile.AccountId);
                        RefreshPlayerVisualization(profile, inventoryController, session, window);
                    }
                    catch (Exception ex)
                    {
                        Modules.Logger.LogError("[UI] Failed to persist teammate tactic change.");
                        Modules.Logger.LogError(ex);
                        ShowProfileSaveFailure();
                    }
                });
            }
            scrollRect.verticalNormalizedPosition = 1f;
            DefaultUIButton closeButton = GameObject.Instantiate(buttonTemplate, panelRect, false);
            closeButton.name = "pitFireTeam_ProfileOptionsClose";
            closeButton.gameObject.SetActive(true);
            closeButton.Interactable = true;
            closeButton.SetRawText(GetSocialUiText("Close"), 16);
            HideProfileButtonIconContainer(closeButton);
            ApplyProfileButtonSurface(closeButton);
            closeButton.OnClick.RemoveAllListeners();
            closeButton.OnClick.AddListener((System.Action)CloseProfileOptionsOverlay);
            RectTransform closeRect = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(closeButton.transform);
            if (closeRect != null)
            {
                closeRect.anchorMin = new Vector2(1f, 0f);
                closeRect.anchorMax = new Vector2(1f, 0f);
                closeRect.pivot = new Vector2(1f, 0f);
                closeRect.anchoredPosition = new Vector2(-18f, 12f);
                closeRect.sizeDelta = new Vector2(160f, 42f);
                closeRect.localScale = Vector3.one;
            }

        }

        private static void CreateProfileOptionsTitle(Transform parent)
        {
            GameObject backgroundObject = new GameObject(
                "pitFireTeam_ProfileOptionsTitleBackground",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Image>());
            backgroundObject.transform.SetParent(parent, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.offsetMin = new Vector2(18f, -56f);
            backgroundRect.offsetMax = new Vector2(-18f, -12f);
            backgroundRect.localScale = Vector3.one;
            Image header = backgroundObject.GetComponent<Image>();
            header.color = new Color(0.04f, 0.23f, 0.30f, 0.96f);
            header.raycastTarget = false;
            CustomTextMeshProUGUI label = CreateOverlayText(
                "pitFireTeam_ProfileOptionsTitleLabel",
                backgroundObject.transform,
                Vector2.zero,
                Vector2.zero,
                TextAlignmentOptions.MidlineLeft,
                GetSocialUiText("ProfileOptionsTitle"),
                22f,
                Color.white);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
        }
        private static void AddProfileOptionsHeader(Transform content, string text)
        {
            GameObject headerObject = new GameObject(
                "pitFireTeam_ProfileOptionsHeader",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<CustomTextMeshProUGUI>(),
                Il2CppInterop.Runtime.Il2CppType.Of<LayoutElement>());
            headerObject.transform.SetParent(content, false);
            RectTransform rect = headerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 38f);
            LayoutElement layout = headerObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 38f;
            CustomTextMeshProUGUI label = headerObject.GetComponent<CustomTextMeshProUGUI>();
            label.text = text;
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Left;
            label.color = Color.white;
        }

        private static DefaultUIButton AddProfileOptionsRow(
            DefaultUIButton template,
            RectTransform content,
            string caption,
            Action onClick)
        {
            if (template == null || content == null || onClick == null)
            {
                return null;
            }

            DefaultUIButton button = GameObject.Instantiate(template, content, false);
            button.name = "pitFireTeam_ProfileOption";
            button.gameObject.SetActive(true);
            button.Interactable = true;
            button.SetRawText(caption, 16);
            HideProfileButtonIconContainer(button);
            ApplyProfileButtonSurface(button);
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((System.Action)onClick);
            RectTransform rect = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(button.transform);
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, 44f);
                rect.localScale = Vector3.one;
            }

            LayoutElement layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 44f;
            layout.preferredHeight = 44f;
            layout.flexibleHeight = 0f;
            return button;
        }

        private static bool IsBetaHiddenTactic(string tactic)
        {
            return string.Equals(tactic, "protector", StringComparison.OrdinalIgnoreCase);
        }

        private static void RefreshPlayerVisualization(ResultProfile profile, InventoryController inventoryController, EFT.IEftSession session, InventoryPlayerModelWithStatsWindow window)
        {
            RefreshPlayerVisualizationAsync(profile, inventoryController, session, window).HandleExceptions();
        }

        private static async Task RefreshPlayerVisualizationAsync(ResultProfile profile, InventoryController inventoryController, EFT.IEftSession session, InventoryPlayerModelWithStatsWindow window)
        {
            try
            {
                Result<OtherProfileResult> result = await session.GetOtherPlayerProfile(profile.AccountId);
                if (result.Failed)
                {
                    throw new InvalidOperationException($"Teammate preview refresh failed: {result.Error}");
                }

                // A pending network response must not repaint a different teammate or a closed screen.
                if (ActiveProfileScreen == null || !ActiveProfileScreen.gameObject.activeInHierarchy
                    || !pitTeam.Compat.Il2CppBoundary.SameNativeObject(ViewedProfile, profile)
                    || !pitTeam.Compat.Il2CppBoundary.SameNativeObject(ActiveProfilePlayerModelWindow, window))
                {
                    return;
                }

                    profile.PlayerVisualRepresentation.Info.Nickname = result.Value.Info?.Nickname ?? profile.PlayerVisualRepresentation.Info.Nickname;
                    profile.PlayerVisualRepresentation.Info.Side = result.Value.Info?.Side ?? profile.PlayerVisualRepresentation.Info.Side;
                    profile.PlayerVisualRepresentation.Customization[EBodyModelPart.Head] = result.Value.Customization[EBodyModelPart.Head];
                    profile.PlayerVisualRepresentation.Customization[EBodyModelPart.Body] = result.Value.Customization[EBodyModelPart.Body];
                    profile.PlayerVisualRepresentation.Customization[EBodyModelPart.Feet] = result.Value.Customization[EBodyModelPart.Feet];
                    profile.PlayerVisualRepresentation.Customization[EBodyModelPart.Hands] = result.Value.Customization[EBodyModelPart.Hands];

                    InventoryEquipment refreshedEquipment = result.Value.Equipment.ToEquipment();

                    profile.PlayerVisualRepresentation.Equipment = refreshedEquipment;
                    profile.Equipment = refreshedEquipment;
                    if (!pitTeam.Compat.Il2CppBoundary.SameNativeObject(profile.PlayerVisualRepresentation.Equipment, refreshedEquipment)
                        || !pitTeam.Compat.Il2CppBoundary.SameNativeObject(profile.Equipment, refreshedEquipment))
                    {
                        throw new InvalidOperationException("Native teammate preview equipment readback did not match the refreshed inventory.");
                    }

                    PlayerModelView playerModelView = window._playerModelView;

                    if (playerModelView?.gameObject.activeSelf == true)
                    {
                        playerModelView.Close();
                    }

                    window.ShowPreview(profile.PlayerVisualRepresentation, inventoryController);
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError($"[UI] Failed to refresh teammate preview for '{profile?.AccountId}'.");
                Modules.Logger.LogError(ex);
                if (ActiveProfileScreen != null && ActiveProfileScreen.gameObject.activeInHierarchy
                    && pitTeam.Compat.Il2CppBoundary.SameNativeObject(ViewedProfile, profile))
                    EFT.Communications.NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfilePreviewRefreshFailed"), ENotificationDurationType.Default);
            }
        }
    }
}
