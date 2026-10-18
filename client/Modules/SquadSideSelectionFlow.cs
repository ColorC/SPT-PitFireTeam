using Comfort.Common;
using EFT;
using EFT.UI.Matchmaker;
using EFT.UI;
using SPTushonka.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using pitTeam.Compat;
using pitTeam.Patches;

namespace pitTeam.Modules
{
    internal static class SquadSideSelectionFlow
    {
        private static readonly NativeUiMemberAccessor MenuPlayButtonAccessor =
            NativeUiMemberAccessor.ForProperty(typeof(MenuScreen), "_playButton", typeof(DefaultUIButton), false, false);

        private const string AlphaLabelPath = "Preloader UI/BottomPanel/Content/UpperPart/AlphaLabel";

        public static bool SquadModeActive { get; private set; }
        public static bool SuppressPlayerModelViewShow { get; private set; }
        public static bool IsOpeningSquadModeScreen { get; private set; }
        public static IReadOnlyCollection<string> OpeningGroupAccountIds => openingGroupAccountIds;

        private static GameObject alphaLabelObject;
        private static bool? alphaLabelWasActive;
        private static bool restoringProfileRoot;
        private static HashSet<string> openingGroupAccountIds = new HashSet<string>(StringComparer.Ordinal);

        public static void Open()
        {
            if (SquadModeActive)
            {
                if (HasActiveSquadSideSelectionScreen())
                {
                    return;
                }

                Deactivate("stale-squad-screen-open");
            }

            List<MenuScreen> activeMenuScreens = Resources.FindObjectsOfTypeAll<MenuScreen>()
                .Where(menu => menu != null && menu.gameObject != null && menu.gameObject.activeInHierarchy)
                .ToList();
            if (activeMenuScreens.Count != 1)
            {
                pitFireTeam.Log.LogWarning($"[SquadFlow] Expected one active MenuScreen for the stock Play event; found {activeMenuScreens.Count}.");
                return;
            }

            MenuScreen menuScreen = activeMenuScreens[0];
            DefaultUIButton playButton = MenuPlayButtonAccessor?.GetValue(menuScreen) as DefaultUIButton;
            if (playButton == null || !playButton.isActiveAndEnabled || !playButton.Interactable || playButton.OnClick == null)
            {
                pitFireTeam.Log.LogWarning("[SquadFlow] Active MenuScreen PlayButton or its OnClick event is unavailable.");
                return;
            }

            TarkovApplication app = ResolveApp();
            if (app == null)
            {
                pitFireTeam.Log.LogWarning("[SquadFlow] TarkovApplication not available.");
                return;
            }

            CaptureOpeningGroupSnapshot(app);
            SquadModeActive = true;
            IsOpeningSquadModeScreen = true;
            HideSquadScreenAlphaLabel();

            try
            {
                // Invoke the same native event that opens character selection from the stock Play button.
                playButton.OnClick.Invoke();
            }
            catch (Exception ex)
            {
                Deactivate("stock-play-button-event-failed");
                pitFireTeam.Log.LogError("[SquadFlow] Failed to invoke the active MenuScreen PlayButton event.");
                pitFireTeam.Log.LogError(ex);
            }
            finally
            {
                IsOpeningSquadModeScreen = false;
            }
        }

        internal enum ProfileReturnTarget
        {
            MainMenuFallback,
            ExistingSideSelectionScreen,
            RejectAmbiguous
        }

        internal static ProfileReturnTarget ResolveProfileReturnTarget(int activeSideSelectionScreenCount)
        {
            if (activeSideSelectionScreenCount < 0 || activeSideSelectionScreenCount > 1)
            {
                return ProfileReturnTarget.RejectAmbiguous;
            }

            return activeSideSelectionScreenCount == 1
                ? ProfileReturnTarget.ExistingSideSelectionScreen
                : ProfileReturnTarget.MainMenuFallback;
        }

        public static void ReopenAfterProfile()
        {
            List<MatchMakerSideSelectionScreen> activeScreens;
            try
            {
                activeScreens = Resources.FindObjectsOfTypeAll<MatchMakerSideSelectionScreen>()
                    .Where(screen => screen != null && screen.gameObject != null && screen.gameObject.activeInHierarchy)
                    .ToList();
            }
            catch (Exception ex)
            {
                pitFireTeam.Log.LogWarning("[SquadFlow] Could not inspect active side-selection screens after profile close.");
                pitFireTeam.Log.LogError(ex);
                return;
            }

            switch (ResolveProfileReturnTarget(activeScreens.Count))
            {
                case ProfileReturnTarget.MainMenuFallback:
                    ReopenFromMainMenuRoot();
                    return;
                case ProfileReturnTarget.RejectAmbiguous:
                    pitFireTeam.Log.LogWarning($"[SquadFlow] Refusing profile return with {activeScreens.Count} active side-selection screens.");
                    return;
            }

            TarkovApplication app = ResolveApp();
            if (app == null)
            {
                pitFireTeam.Log.LogWarning("[SquadFlow] TarkovApplication unavailable while restoring the active side-selection screen.");
                return;
            }

            CaptureOpeningGroupSnapshot(app);
            SquadModeActive = true;
            IsOpeningSquadModeScreen = false;
            HideSquadScreenAlphaLabel();
            BeginPlayerModelViewSuppression();
            MatchMakerSideSelectionScreenShowPatch.ApplySquadMode(activeScreens[0]);
        }

        private static void ReopenFromMainMenuRoot()
        {
            if (restoringProfileRoot) return;
            if (Resources.FindObjectsOfTypeAll<MenuScreen>().Any(menu => menu != null && menu.gameObject.activeInHierarchy))
            {
                Open();
                return;
            }

            // A profile reopened by the gear shop has the builds screen beneath it.
            // Pop that history through the native manager before invoking the menu Play event.
            restoringProfileRoot = true;
            try
            {
                EFT.UI.Screens.EftScreenManager.Instance.TryReturnToRootScreen()
                    .ContinueWith((System.Action<Il2CppSystem.Threading.Tasks.Task<bool>>)(task =>
                    {
                        restoringProfileRoot = false;
                        try
                        {
                        if (task.IsFaulted || task.IsCanceled || !task.Result)
                        {
                            ReportProfileReturnFailure(task.Exception?.ToString());
                            return;
                        }
                        if (!Resources.FindObjectsOfTypeAll<MenuScreen>().Any(menu => menu != null && menu.gameObject.activeInHierarchy))
                        {
                            ReportProfileReturnFailure("Native root transition completed without an active menu.");
                            return;
                        }
                        Open();
                        }
                        catch (Exception ex) { ReportProfileReturnFailure(ex.ToString()); }
                    }), Il2CppSystem.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                restoringProfileRoot = false;
                ReportProfileReturnFailure(ex.ToString());
            }
        }

        private static void ReportProfileReturnFailure(string error)
        {
            pitFireTeam.Log.LogError("[SquadFlow] Could not return to the main menu root after teammate profile close. " + error);
            EFT.Communications.NotificationManager.DisplayWarningNotification(
                pitFireTeam.GetSocialUiText("SquadControlReturnFailed"), EFT.Communications.ENotificationDurationType.Default);
        }
        public static void OnScreenClosed()
        {
            IsOpeningSquadModeScreen = false;
            if (SquadModeActive)
            {
                Deactivate("side-selection-screen-closed");
                return;
            }

            SuppressPlayerModelViewShow = false;
            ShowSquadScreenAlphaLabel();
        }

        public static void Deactivate(string reason = null)
        {
            SuppressPlayerModelViewShow = false;
            SquadModeActive = false;
            IsOpeningSquadModeScreen = false;
            ShowSquadScreenAlphaLabel();
            openingGroupAccountIds.Clear();

            if (!string.IsNullOrWhiteSpace(reason))
            {
                Logger.LogInfo($"[SquadFlow] Squad side-selection mode disabled: {reason}");
            }
        }

        public static void BeginPlayerModelViewSuppression()
        {
            SuppressPlayerModelViewShow = true;
        }

        public static void EndPlayerModelViewSuppression()
        {
            SuppressPlayerModelViewShow = false;
        }

        private static void HideSquadScreenAlphaLabel()
        {
            GameObject target = ResolveAlphaLabelObject();
            if (target == null)
            {
                return;
            }

            alphaLabelWasActive ??= target.activeSelf;
            target.SetActive(false);
        }

        private static void ShowSquadScreenAlphaLabel()
        {
            if (alphaLabelObject == null || !alphaLabelWasActive.HasValue)
            {
                return;
            }

            alphaLabelObject.SetActive(alphaLabelWasActive.Value);
            alphaLabelWasActive = null;
        }

        private static GameObject ResolveAlphaLabelObject()
        {
            if (alphaLabelObject != null)
            {
                return alphaLabelObject;
            }

            if (!MonoBehaviourSingleton<PreloaderUI>.Instantiated)
            {
                return null;
            }

            Transform transform = MonoBehaviourSingleton<PreloaderUI>.Instance.transform.Find(AlphaLabelPath);
            if (transform == null)
            {
                return null;
            }

            alphaLabelObject = transform.gameObject;
            return alphaLabelObject;
        }

        private static TarkovApplication ResolveApp()
        {
            if (pitFireTeam.application != null)
            {
                return pitFireTeam.application;
            }

            try
            {
                pitFireTeam.application = ClientAppUtils.GetMainApp();
            }
            catch { }

            return pitFireTeam.application;
        }

        private static bool HasActiveSquadSideSelectionScreen()
        {
            try
            {
                return Resources.FindObjectsOfTypeAll<MatchMakerSideSelectionScreen>()
                    .Any(screen => screen != null && screen.gameObject != null && screen.gameObject.activeInHierarchy);
            }
            catch (Exception ex)
            {
                pitFireTeam.Log.LogWarning("[SquadFlow] Failed to verify active squad side-selection screen.");
                pitFireTeam.Log.LogError(ex);
                return false;
            }
        }

        private static void CaptureOpeningGroupSnapshot(TarkovApplication app)
        {
            openingGroupAccountIds.Clear();

            EFT.UI.Matchmaker.MatchmakerPlayersController controller = app?.Matchmaker;
            if (controller?.GroupPlayers == null)
            {
                return;
            }

            for (int i = 0; i < controller.GroupPlayers.Count; i++)
            {
                string accountId = controller.GroupPlayers[i]?.AccountId;
                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    openingGroupAccountIds.Add(accountId);
                }
            }
        }

        public static bool IsAccountInOpeningGroupSnapshot(string accountId)
        {
            return !string.IsNullOrWhiteSpace(accountId) && openingGroupAccountIds.Contains(accountId);
        }
    }
}
