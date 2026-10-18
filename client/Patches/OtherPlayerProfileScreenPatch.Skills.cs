using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using ResultProfile = EFT.OtherPlayerProfile;

namespace pitTeam.Patches
{
    internal partial class OtherPlayerProfileScreenPatch
    {
        internal static object SkillsCloneProbe { get; private set; }

        private static void DisplaySkillsPanel(OtherPlayerProfileScreen screen, ResultProfile profile, InventoryController inventoryController, EFT.IEftSession session, FriendlyTeammateProfileOptions options)
        {
            if (screen == null || profile?.Skills == null || session?.Profile == null)
            {
                pitFireTeam.Log.LogWarning("[UI] Skills panel skipped: missing screen, profile skills, or session profile.");
                return;
            }

            SkillsScreen template = FindSkillsScreenTemplate();
            if (template == null || !TryPrepareSkillsHost(screen, out RectTransform hostParent))
            {
                pitFireTeam.Log.LogWarning($"[UI] Skills panel skipped: template={(template != null)}.");
                EFT.Communications.NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileSkillsUnavailable"), ENotificationDurationType.Default);
                return;
            }

            if (SkillsPanel != null)
            {
                GameObject.Destroy(SkillsPanel.gameObject);
                SkillsPanel = null;
            }

            if (SkillsPanelHost != null)
            {
                GameObject.Destroy(SkillsPanelHost.gameObject);
                SkillsPanelHost = null;
            }

            GameObject hostObject = new GameObject("pitFireTeam_ProfileSkillsHost", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            hostObject.transform.SetParent(hostParent, false);
            RectTransform hostRect = hostObject.GetComponent<RectTransform>();
            hostObject.AddComponent<LayoutElement>().ignoreLayout = true;
            StretchToFillParent(hostRect);
            hostRect.SetAsLastSibling();

            SkillsScreen clone = GameObject.Instantiate(template, hostRect, false);
            clone.name = "pitFireTeam_ProfileSkillsScreen";
            int inheritedListViews, inheritedGridViews;
            try
            {
                inheritedListViews = RemoveInheritedSkillViews(clone.transform, clone._skillList._skillsContainer, clone._skillList._skillViewTemplate);
                inheritedGridViews = RemoveInheritedSkillViews(clone.transform, clone._skillThumbs._skillsContainer, clone._skillThumbs._skillViewTemplate);
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("[UI] Refused skill clone cleanup: " + ex);
                NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileSkillsUnavailable"), ENotificationDurationType.Default);
                GameObject.Destroy(hostObject);
                return;
            }
            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(clone.transform) is RectTransform cloneRect)
            {
                ConfigureInjectedSkillsScreenRect(screen, cloneRect);
            }

            if (!Spt5FollowerHealthControllerFactory.TryCreate(profile, inventoryController, options?.FollowerHealth, out Profile.HealthInfo followerHealth, out IHealthController healthController, out string healthError, out string healthDiagnostic))
            {
                string diagnosticSuffix = string.IsNullOrWhiteSpace(healthDiagnostic) ? string.Empty : "; " + healthDiagnostic;
                pitFireTeam.Log.LogWarning("[UI] Skills panel skipped: follower-owned native health controller unavailable: " + healthError + diagnosticSuffix);
                EFT.Communications.NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileSkillsUnavailable"), ENotificationDurationType.Default);
                GameObject.Destroy(hostObject);
                GameObject.Destroy(clone.gameObject);
                return;
            }

            if (!string.IsNullOrWhiteSpace(healthDiagnostic)) pitFireTeam.Log.LogInfo("[UI] Follower skills health context: " + healthDiagnostic);

            Profile skillsProfile = BuildSkillsProfile(session.Profile, profile.Skills);
            skillsProfile.Health = followerHealth;
            if (pitTeam.Compat.Spt5RuntimeInstallProbe.Enabled)
                SkillsCloneProbe = new { accountId = profile.AccountId, inheritedListViews, inheritedGridViews,
                    skillIds = Enumerable.Range(0, skillsProfile.Skills.Skills.Length).Select(index => skillsProfile.Skills.Skills[index]?.Id.ToString()).ToArray(),
                    displayIds = Enumerable.Range(0, skillsProfile.Skills.DisplayList.Length).Select(index => skillsProfile.Skills.DisplayList[index]?.Id.ToString()).ToArray() };

            if (!TryInitializeSkillsScreen(clone))
            {
                NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileSkillsUnavailable"), ENotificationDurationType.Default);
                GameObject.Destroy(hostObject);
                GameObject.Destroy(clone.gameObject);
                return;
            }

            try
            {
                SkillsScreenShowMethod?.Invoke(clone, new object[] { skillsProfile, healthController });
                ConfigureInjectedSkillsScreenRect(screen, clone.GetComponent<RectTransform>());
                EnsureSkillsScreenOptionsVisible(clone);
                ConfigureSkillsScrollArea(clone);
                HideDetailedSkillProgressChildren(clone.transform);
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("[UI] Failed to show follower skills panel.");
                Modules.Logger.LogError(ex);
                EFT.Communications.NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileSkillsUnavailable"), ENotificationDurationType.Default);
                GameObject.Destroy(hostObject);
                GameObject.Destroy(clone.gameObject);
                return;
            }

            EFT.UI.UIParent ui = UiAccessor.GetValue(screen) as EFT.UI.UIParent;
            ui?.AddDisposable(clone);

            SkillsPanelHost = hostRect;
            SkillsPanel = clone;
            Modules.Logger.LogInfo($"[UI] Profile skills panel shown for '{profile.AccountId}'.");
        }

        private static int RemoveInheritedSkillViews<T>(Transform cloneRoot, Transform container, T template) where T : Component
        {
            if (container == null) return 0;
            if (cloneRoot == null || (container.Pointer != cloneRoot.Pointer && !container.IsChildOf(cloneRoot)))
                throw new InvalidOperationException($"Skill container is outside the cloned screen: clone={cloneRoot?.name}:{cloneRoot?.Pointer}, container={container.name}:{container.Pointer}, template={template?.name}:{template?.Pointer}.");
            if (template == null || (template.transform.Pointer != cloneRoot.Pointer && !template.transform.IsChildOf(cloneRoot)))
                throw new InvalidOperationException($"Skill template is missing or outside the cloned screen: clone={cloneRoot.name}:{cloneRoot.Pointer}, template={template?.name}:{template?.Pointer}.");
            int removed = 0;
            foreach (var view in container.GetComponentsInChildren<T>(true))
            {
                if (view == null || pitTeam.Compat.Il2CppBoundary.SameNativeObject(view, template)
                    || (template != null && view.transform.IsChildOf(template.transform))) continue;
                // Instantiate copied generated stock item objects as well as the serialized
                // view template. The clone's native Show will create its own bound items.
                view.gameObject.SetActive(false);
                GameObject.Destroy(view.gameObject);
                removed++;
            }
            return removed;
        }

        private static Profile BuildSkillsProfile(Profile sessionProfile, SkillManager sourceSkills)
        {
            Profile skillsProfile = sessionProfile?.Clone();
            if (skillsProfile?.Skills == null || sourceSkills == null)
            {
                return skillsProfile;
            }

            skillsProfile.Skills.ApplyChanges(sourceSkills);
            FilterHiddenSkills(skillsProfile.Skills);
            return skillsProfile;
        }

        private static void FilterHiddenSkills(SkillManager skillManager)
        {
            if (skillManager == null)
            {
                return;
            }

            ReplaceSkillArray(SkillManagerDisplayListAccessor, skillManager);
            ReplaceSkillArray(SkillManagerSkillsAccessor, skillManager);
        }

        private static void ReplaceSkillArray(NativeUiMemberAccessor accessor, SkillManager skillManager)
        {
            if (accessor?.GetValue(skillManager) is not Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.Skill> nativeSkills)
            {
                return;
            }

            EFT.Skill[] filtered = Enumerable.Range(0, nativeSkills.Length).Select(index => nativeSkills[index])
                .Where(skill => skill != null && !skill.Locked && !HiddenFollowerSkills.Contains(skill.Id))
                .ToArray();

            var nativeFiltered = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.Skill>(filtered.Length);
            for (int index = 0; index < filtered.Length; index++) nativeFiltered[index] = filtered[index];
            accessor.SetValue(skillManager, nativeFiltered);
        }

        private static void HideDetailedSkillProgressChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == "Progress")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static bool TryInitializeSkillsScreen(SkillsScreen skillsScreen)
        {
            if (skillsScreen == null)
            {
                return false;
            }

            if (SkillsScreenTabsControllerAccessor.GetValue(skillsScreen) != null)
            {
                return true;
            }

            try
            {
                // Let Unity own Awake. Manually invoking it while this clone is
                // inactive leaves a second initialization when native Show activates it.
                skillsScreen.gameObject.SetActive(true);
                if (!skillsScreen.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("The cloned skills screen has an inactive parent.");
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("[UI] Failed to initialize stock SkillsScreen clone.");
                Modules.Logger.LogError(ex);
                return false;
            }

            return SkillsScreenTabsControllerAccessor.GetValue(skillsScreen) != null;
        }

        private static SkillsScreen FindSkillsScreenTemplate()
        {
            SkillsScreen direct = Resources.FindObjectsOfTypeAll<SkillsScreen>()
                .FirstOrDefault(screen =>
                    screen != null &&
                    screen.name != "pitFireTeam_ProfileSkillsScreen" &&
                    pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(screen.transform) != null);
            if (direct != null)
            {
                return direct;
            }

            SkillsAndMasteringScreen skillsAndMastering = Resources.FindObjectsOfTypeAll<SkillsAndMasteringScreen>()
                .FirstOrDefault(screen => screen != null);
            SkillsScreen fromSkillsAndMastering = SkillsAndMasteringSkillsScreenAccessor.GetValue(skillsAndMastering) as SkillsScreen;
            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(fromSkillsAndMastering?.transform) != null)
            {
                return fromSkillsAndMastering;
            }

            InventoryScreen inventoryScreen = Resources.FindObjectsOfTypeAll<InventoryScreen>()
                .FirstOrDefault(screen => screen != null);
            SkillsAndMasteringScreen inventorySkillsAndMastering = InventorySkillsAndMasteringScreenAccessor.GetValue(inventoryScreen) as SkillsAndMasteringScreen;
            SkillsScreen fromInventory = SkillsAndMasteringSkillsScreenAccessor.GetValue(inventorySkillsAndMastering) as SkillsScreen;
            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(fromInventory?.transform) != null)
            {
                return fromInventory;
            }

            pitFireTeam.Log.LogWarning("[UI] Unable to locate a stock SkillsScreen template.");
            return null;
        }

        private static bool TryPrepareSkillsHost(OtherPlayerProfileScreen screen, out RectTransform hostParent)
        {
            hostParent = null;
            if (screen == null)
            {
                return false;
            }

            Transform rightSide = screen.transform.Find("RightSide")
                ?? FindChildRecursive(screen.transform, "RightSide");

            rightSide?.gameObject.SetActive(true);
            hostParent = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(rightSide);
            return hostParent != null;
        }

        private static void EnsureSkillsScreenOptionsVisible(SkillsScreen skillsScreen)
        {
            if (skillsScreen == null)
            {
                return;
            }

            Transform options = skillsScreen.transform.Find("Options")
                ?? FindChildRecursive(skillsScreen.transform, "Options");
            if (options == null)
            {
                return;
            }

            options.gameObject.SetActive(true);
            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(options) is RectTransform optionsRect)
            {
                optionsRect.anchorMin = new Vector2(0f, 1f);
                optionsRect.anchorMax = new Vector2(1f, 1f);
                optionsRect.pivot = new Vector2(0.5f, 1f);
                optionsRect.localScale = Vector3.one;
                optionsRect.anchoredPosition = Vector2.zero;
                optionsRect.sizeDelta = new Vector2(0f, 40f);
            }
        }

        private static void ConfigureSkillsScrollArea(SkillsScreen skillsScreen)
        {
            var lower = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(skillsScreen.transform.Find("Lower Part"));
            if (lower != null)
            {
                StretchToFillParent(lower);
                lower.offsetMax = new Vector2(0f, -44f);
            }
            var scroll = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(skillsScreen.transform.Find("Lower Part/Scrollview"));
            if (scroll != null)
            {
                StretchToFillParent(scroll);
            }
        }

        private static void StretchToFillParent(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = new Vector2(0f, 0f);
            target.anchorMax = new Vector2(1f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = Vector2.zero;
            target.sizeDelta = Vector2.zero;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;
        }

        private static void ConfigureInjectedSkillsScreenRect(OtherPlayerProfileScreen screen, RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            // The inventory template's fixed offsets belong to a different parent layout.
            // Size this clone from its actual profile host and reserve the action rows above.
            StretchToFillParent(target);
            target.offsetMin = new Vector2(12f, 16f);
            target.offsetMax = new Vector2(-12f, -(ProfileActionsHeight + 12f));
        }
    }
}
