using Comfort.Common;
using SPTushonka.Common.Utils;
using Arena.UI;
using EFT;
using EFT.Communications;
using EFT.UI;
using EFT.UI.Screens;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Newtonsoft.Json;
using SPTushonka.Common.Http;
using SPTushonka.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using pitTeam.Patches;
using pitTeam.Compat;

namespace pitTeam.Modules
{
    internal static class AddTeammateCreationFlow
    {
        private const float MinimumReturnLoaderDurationSeconds = 0.1f;

        private static readonly Type SideSelectionScreenType = typeof(AccountSideSelectionScreen<EftAccountSideSelectionScreen.EftAccountSideSelectionScreenController, EEftScreenType>);
        private static readonly System.Reflection.MethodInfo AdvanceStateMethod = AccessTools.Method(SideSelectionScreenType, "ShowAnotherState");
        private static readonly NativeUiMemberAccessor CurrentStateIndexAccessor = NativeUiMemberAccessor.ForProperty(SideSelectionScreenType, "_currentSelectedStateIndex", typeof(int), false, false);
        private static readonly NativeUiMemberAccessor SideSelectionStateAccessor = NativeUiMemberAccessor.ForProperty(SideSelectionScreenType, "_sideSelectionState", typeof(SideSelectionState), false, false);
        private static readonly NativeUiMemberAccessor DescriptionCanvasGroupAccessor = NativeUiMemberAccessor.ForProperty(typeof(SideSelectionState), "_descriptionCanvasGroup", typeof(CanvasGroup), false, false);
        private static readonly NativeUiMemberAccessor SelectSideNodesAccessor = NativeUiMemberAccessor.ForProperty(typeof(SideSelectionState), "_selectSideNodes", typeof(Il2CppSystem.Collections.Generic.List<SideSelectionState.SelectSideNode>), false, false);
        private static readonly NativeUiMemberAccessor BackButtonField = NativeUiMemberAccessor.ForProperty(SideSelectionScreenType, "_backButton", typeof(DefaultUIButton), false, false);
        private static readonly NativeUiMemberAccessor HeadSelectionStateAccessor = NativeUiMemberAccessor.ForProperty(SideSelectionScreenType, "_headSelectionState", typeof(HeadSelectionState), false, false);
        private static readonly NativeUiMemberAccessor NextButtonAccessor = NativeUiMemberAccessor.ForProperty(SideSelectionScreenType, "_nextButton", typeof(DefaultUIButton), false, false);
        private static readonly NativeUiMemberAccessor NicknameInputAccessor = NativeUiMemberAccessor.ForProperty(typeof(NicknameField), "_inputField", typeof(TMP_InputField), false, false);
        private static EftAccountSideSelectionScreen.EftAccountSideSelectionScreenController activeController;
        private static Coroutine advanceCoroutine;
        private static Coroutine returnCoroutine;
        private static Coroutine submitCoroutine;
        private static bool skipScheduled;
        private static bool submitInProgress;
        private static bool startInProgress;
        private static bool returnInProgress;
        private static bool nicknamePrepared;
        private static bool suppressSkippedSideSelectionModelView;
        private static Button wiredNextButton;
        private static Button wiredBackButton;
        private static UnityAction nextButtonAction;
        private static UnityAction backButtonAction;
        private static readonly Il2CppSystem.Action<string> NicknameSubmittedToken = (Il2CppSystem.Action<string>)(System.Action<string>)HandleNicknameSubmitted;
        private static readonly Il2CppSystem.Action FlowCompletedToken = (Il2CppSystem.Action)(System.Action)HandleFlowCompleted;
        private static readonly Il2CppSystem.Action ScreenShownToken = (Il2CppSystem.Action)(System.Action)HandleScreenShown;
        private static readonly Il2CppSystem.Action FlowClosedToken = (Il2CppSystem.Action)(System.Action)CleanupActiveFlow;
        private static Action pendingReturnAction;

        public static bool IsActive => activeController != null;
        public static bool SuppressSkippedSideSelectionModelView => suppressSkippedSideSelectionModelView;

        public static void Start(Action onReturn = null)
        {
            if (activeController != null || startInProgress)
            {
                ShowToast(GetLocalizedSocialUi("AddTeammateFlowActive"));
                return;
            }

            pendingReturnAction = onReturn;
            startInProgress = true;
            StartInternal().HandleExceptions();
        }

        public static bool IsActiveForController(object controller)
        {
            return activeController != null && controller is Il2CppObjectBase native && native.Pointer == activeController.Pointer;
        }

        public static void ReturnToMainScreen()
        {
            if (pendingReturnAction != null)
            {
                if (pitFireTeam.Instance == null)
                {
                    EFT.UI.Screens.EftScreenManager.Instance.TryReturnToRootScreen().HandleExceptions();
                    InvokePendingReturnAction();
                    return;
                }

                if (returnCoroutine != null)
                {
                    pitFireTeam.Instance.StopCoroutine(returnCoroutine);
                }

                returnInProgress = true;
                returnCoroutine = pitFireTeam.Instance.StartCoroutine(ReturnToPendingScreen());
                return;
            }

            if (pitFireTeam.Instance == null)
            {
                EFT.UI.Screens.EftScreenManager.Instance.TryReturnToRootScreen().HandleExceptions();
                InvokePendingReturnAction();
                return;
            }

            if (returnCoroutine != null)
            {
                pitFireTeam.Instance.StopCoroutine(returnCoroutine);
            }

            returnInProgress = true;
            returnCoroutine = pitFireTeam.Instance.StartCoroutine(ReturnToMainScreenWithOverlay());
        }

        public static void RefreshSubmitButton()
        {
            if (!MonoBehaviourSingleton<LoginUI>.Instantiated)
            {
                return;
            }

            ConfigureHeadSelectionUi(MonoBehaviourSingleton<LoginUI>.Instance.SideSelectionScreen);
        }

        public static bool TryCompleteFromCurrentScreen()
        {
            if (submitInProgress)
            {
                return false;
            }

            if (!MonoBehaviourSingleton<LoginUI>.Instantiated)
            {
                return false;
            }

            EftAccountSideSelectionScreen screen = MonoBehaviourSingleton<LoginUI>.Instance.SideSelectionScreen;
            if (screen == null)
            {
                return false;
            }

            HeadSelectionState headSelectionState = HeadSelectionStateAccessor?.GetValue(screen) as HeadSelectionState;
            if (headSelectionState == null || headSelectionState._profileData == null)
            {
                return false;
            }

            string nickname = GetNicknameText(headSelectionState);
            ENicknameError error = headSelectionState._nicknameField != null
                ? headSelectionState._nicknameField.ValidationError(nickname)
                : ENicknameError.InvalidNickname;

            if (error != ENicknameError.ValidNickname)
            {
                headSelectionState._nicknameField?.ShowNicknameError(error, false);
                ConfigureHeadSelectionUi(screen);
                return false;
            }

            headSelectionState._profileData.Nickname = nickname;

            FriendlyTeammateCreateRequest payload = new FriendlyTeammateCreateRequest
            {
                nickname = headSelectionState._profileData.Nickname,
                voice = headSelectionState._profileData.VoiceId,
                head = headSelectionState._profileData.HeadId
            };

            string json = pitTeam.Compat.SptJsonBoundary.Serialize(payload);
            Logger.LogInfo($"[UI] Add teammate selection complete: {json}");

            submitInProgress = true;
            ConfigureHeadSelectionUi(screen);

            if (submitCoroutine != null && pitFireTeam.Instance != null)
            {
                pitFireTeam.Instance.StopCoroutine(submitCoroutine);
            }

            submitCoroutine = pitFireTeam.Instance.StartCoroutine(SubmitTeammateCoroutine(payload));
            return true;
        }

        private static async Task StartInternal()
        {
            try
            {
                TarkovApplication app = ResolveApplication();
                EFT.IEftSession session = app?.Session;
                if (session == null)
                {
                    ShowToast(GetLocalizedSocialUi("AddTeammateOpenFailed"));
                    pitFireTeam.Log.LogError("[UI] Could not start add teammate flow: session is not available.");
                    return;
                }

                EPlayerSide playerSide = session.Profile?.Info?.Side ?? EPlayerSide.Usec;
                if (playerSide != EPlayerSide.Bear && playerSide != EPlayerSide.Usec)
                {
                    ShowToast(GetLocalizedSocialUi("AddTeammateUnsupportedSide"));
                    pitFireTeam.Log.LogWarning($"[UI] Add teammate flow aborted because current side is {playerSide}.");
                    return;
                }

                if (MonoBehaviourSingleton<LoginUI>.Instantiated)
                {
                    MonoBehaviourSingleton<LoginUI>.Instance.gameObject.SetActive(true);
                }

                await EnsureDefaultProfilesLoaded(session);
                if (EFT.CreateProfileOperation._bearProfile == null || EFT.CreateProfileOperation._usecProfile == null)
                {
                    ShowToast(GetLocalizedSocialUi("AddTeammateOpenFailed"));
                    pitFireTeam.Log.LogError("[UI] Could not start add teammate flow: default preview profiles are missing.");
                    return;
                }

                EFT.CreateProfileOperation.PreliminaryProfileData profileData = new EFT.CreateProfileOperation.PreliminaryProfileData
                {
                    Side = playerSide
                };

                var controller = new EftAccountSideSelectionScreen.EftAccountSideSelectionScreenController(
                    EFT.CreateProfileOperation._bearProfile,
                    EFT.CreateProfileOperation._usecProfile,
                    profileData,
                    session,
                    false,
                    string.Empty);

                controller.OnNickNameSubmitted += NicknameSubmittedToken;
                controller.OnShowNextScreen += FlowCompletedToken;
                controller.OnShow += ScreenShownToken;
                controller.OnClose += FlowClosedToken;

                activeController = controller;
                skipScheduled = false;
                nicknamePrepared = false;
                suppressSkippedSideSelectionModelView = true;
                SquadSideSelectionFlow.EndPlayerModelViewSuppression();
                controller.ShowScreen(EScreenState.Queued);
            }
            catch (Exception ex)
            {
                CleanupActiveFlow();
                ShowToast(GetLocalizedSocialUi("AddTeammateOpenFailed"));
                pitFireTeam.Log.LogError("[UI] Failed to open add teammate creation flow.");
                pitFireTeam.Log.LogError(ex);
            }
            finally
            {
                startInProgress = false;
            }
        }

        private static async Task EnsureDefaultProfilesLoaded(IClientSession session)
        {
            // Existing accounts never enter stock character creation, so its static
            // previews can remain empty. Load real native profiles through the same
            // verified session API used by follower profile loading, without creating
            // a player account or saving a teammate before the appearance form submits.
            Profile bear = EFT.CreateProfileOperation._bearProfile;
            Profile usec = EFT.CreateProfileOperation._usecProfile;
            if (bear != null && usec != null) return;
            var waves = new Il2CppSystem.Collections.Generic.List<CountTypeBotWave>();
            if (bear == null) waves.Add(new CountTypeBotWave(1, WildSpawnType.pmcBEAR, BotDifficulty.normal));
            if (usec == null) waves.Add(new CountTypeBotWave(1, WildSpawnType.pmcUSEC, BotDifficulty.normal));
            var profiles = await Il2CppTaskBridge.FromNative(session.LoadBots(waves));
            ManagedFramePump.RequireOwnerThread();
            if (profiles != null)
                for (int i = 0; i < profiles.Length; i++)
                {
                    Profile profile = profiles[i];
                    if (profile?.Info?.Settings == null) continue;
                    // SPT deliberately sends PMC bot profiles with Side=Savage.
                    // Its normal BotCreationData.ChooseProfile patch restores Side
                    // from Role only at spawn time; a menu preview never spawns.
                    if (profile.Info.Settings.Role == WildSpawnType.pmcBEAR && bear == null)
                    {
                        profile.Info.Side = EPlayerSide.Bear;
                        bear = profile;
                    }
                    if (profile.Info.Settings.Role == WildSpawnType.pmcUSEC && usec == null)
                    {
                        profile.Info.Side = EPlayerSide.Usec;
                        usec = profile;
                    }
                }
            if (bear == null || usec == null)
                throw new InvalidOperationException("Native session did not return both PMC sides for teammate previews.");
            EFT.CreateProfileOperation._bearProfile = bear;
            EFT.CreateProfileOperation._usecProfile = usec;
        }

        private static void HandleNicknameSubmitted(string nickname)
        {
            activeController?.ShowNicknameError(ENicknameError.ValidNickname);
        }

        private static void HandleScreenShown()
        {
            if (skipScheduled || activeController == null || pitFireTeam.Instance == null)
            {
                return;
            }

            skipScheduled = true;
            advanceCoroutine = pitFireTeam.Instance.StartCoroutine(AdvanceToHeadSelection());
        }

        private static IEnumerator AdvanceToHeadSelection()
        {
            int attemptsRemaining = 60;
            while (attemptsRemaining-- > 0 && activeController != null)
            {
                if (MonoBehaviourSingleton<LoginUI>.Instantiated)
                {
                    EftAccountSideSelectionScreen screen = MonoBehaviourSingleton<LoginUI>.Instance.SideSelectionScreen;
                    if (screen != null)
                    {
                        int currentStateIndex = CurrentStateIndexAccessor?.GetValue(screen) as int? ?? -1;
                        if (currentStateIndex == 0)
                        {
                            try
                            {
                                AdvanceToHeadSelectionAsync(screen).HandleExceptions();
                            }
                            catch (Exception ex)
                            {
                                pitFireTeam.Log.LogError("[UI] Failed to skip side selection for add teammate flow.");
                                pitFireTeam.Log.LogError(ex);
                            }
                            break;
                        }

                        if (currentStateIndex == 1)
                        {
                            HideSkippedSideSelectionVisuals(screen);
                            ConfigureHeadSelectionUi(screen);
                            break;
                        }
                    }
                }

                yield return null;
            }

            advanceCoroutine = null;
        }

        private static async Task AdvanceToHeadSelectionAsync(EftAccountSideSelectionScreen screen)
        {
            IntPtr owner = activeController?.Pointer ?? IntPtr.Zero;
            var advanceTask = AdvanceStateMethod?.Invoke(screen, new object[] { 1 }) as Il2CppSystem.Threading.Tasks.Task
                ?? throw new InvalidOperationException("Native side-selection transition task is unavailable.");
            await Il2CppTaskBridge.FromNative(advanceTask);
            ManagedFramePump.RequireOwnerThread();
            if (owner == IntPtr.Zero || activeController?.Pointer != owner) return;
            HideSkippedSideSelectionVisuals(screen);
            ConfigureHeadSelectionUi(screen);
        }

        private static void HandleFlowCompleted()
        {
            TryCompleteFromCurrentScreen();
        }

        private static void CleanupActiveFlow()
        {
            if (activeController != null)
            {
                activeController.OnNickNameSubmitted -= NicknameSubmittedToken;
                activeController.OnShowNextScreen -= FlowCompletedToken;
                activeController.OnShow -= ScreenShownToken;
                activeController.OnClose -= FlowClosedToken;
            }

            if (advanceCoroutine != null && pitFireTeam.Instance != null)
            {
                pitFireTeam.Instance.StopCoroutine(advanceCoroutine);
            }

            if (returnCoroutine != null && pitFireTeam.Instance != null && !returnInProgress)
            {
                pitFireTeam.Instance.StopCoroutine(returnCoroutine);
            }

            if (submitCoroutine != null && pitFireTeam.Instance != null)
            {
                pitFireTeam.Instance.StopCoroutine(submitCoroutine);
            }

            advanceCoroutine = null;
            returnCoroutine = null;
            submitCoroutine = null;
            activeController = null;
            skipScheduled = false;
            submitInProgress = false;
            nicknamePrepared = false;
            suppressSkippedSideSelectionModelView = false;
            wiredNextButton = null;
            wiredBackButton = null;
            nextButtonAction = null;
            backButtonAction = null;
            if (returnCoroutine == null && !returnInProgress)
            {
                pendingReturnAction = null;
            }
        }

        private static TarkovApplication ResolveApplication()
        {
            if (pitFireTeam.application != null)
            {
                return pitFireTeam.application;
            }

            try
            {
                pitFireTeam.application = ClientAppUtils.GetMainApp();
            }
            catch
            {
            }

            if (pitFireTeam.application == null && Singleton<ClientApplication<EFT.IEftSession>>.Instantiated)
            {
                pitFireTeam.application = Singleton<ClientApplication<EFT.IEftSession>>.Instance as TarkovApplication;
            }

            return pitFireTeam.application;
        }

        internal static void ShowToast(string message)
        {
            EFT.Communications.NotificationManager.DisplayMessageNotification(
                message,
                ENotificationDurationType.Default,
                ENotificationIconType.Default,
                new Il2CppSystem.Nullable<UnityEngine.Color>());
        }

        public static string GetLocalizedSocialUi(string key)
        {
            return pitFireTeam.GetSocialUiText(key);
        }

        private static IEnumerator ReturnToMainScreenWithOverlay()
        {
            PreloaderUI preloaderUi = MonoBehaviourSingleton<PreloaderUI>.Instantiated
                ? MonoBehaviourSingleton<PreloaderUI>.Instance
                : null;

            preloaderUi?.SetLoaderStatus(true);
            var returnTask = EFT.UI.Screens.EftScreenManager.Instance.TryReturnToRootScreen();
            float elapsed = 0f;

            while (!returnTask.IsCompleted || elapsed < MinimumReturnLoaderDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (returnTask.IsFaulted)
            {
                pitFireTeam.Log.LogError("[UI] Failed while returning from add teammate flow to root screen.");
                pitFireTeam.Log.LogError(returnTask.Exception);
            }

            InvokePendingReturnAction();
            yield return null;

            if (preloaderUi != null)
            {
                preloaderUi.SetLoaderStatus(false);
            }

            returnCoroutine = null;
            returnInProgress = false;
        }

        private static IEnumerator ReturnToPendingScreen()
        {
            // Forced CloseSelf can leave every underlying screen hidden in SPT5.
            // Use the screen manager's normal root transition before reopening squad UI.
            var returnTask = EFT.UI.Screens.EftScreenManager.Instance.TryReturnToRootScreen();

            while (!returnTask.IsCompleted)
            {
                yield return null;
            }

            if (returnTask.IsFaulted)
            {
                pitFireTeam.Log.LogError("[UI] Failed while returning from add teammate flow to pending screen.");
                pitFireTeam.Log.LogError(returnTask.Exception?.GetBaseException()?.Message ?? "Unknown close failure.");
                returnCoroutine = null;
                returnInProgress = false;
                yield break;
            }

            if (returnTask.IsCanceled || !returnTask.Result)
            {
                pitFireTeam.Log.LogWarning("[UI] Root-screen return was canceled or declined; squad reopening was not dispatched.");
                returnCoroutine = null;
                returnInProgress = false;
                yield break;
            }
            yield return null;
            InvokePendingReturnAction();

            returnCoroutine = null;
            returnInProgress = false;
        }

        private static void HideSkippedSideSelectionVisuals(EftAccountSideSelectionScreen screen)
        {
            try
            {
                SideSelectionState sideSelectionState = SideSelectionStateAccessor?.GetValue(screen) as SideSelectionState;
                if (sideSelectionState == null)
                {
                    return;
                }

                Component descriptionCanvas = DescriptionCanvasGroupAccessor?.GetValue(sideSelectionState) as Component;
                if (descriptionCanvas != null)
                {
                    descriptionCanvas.gameObject.SetActive(false);
                }

                // Native lists implement Il2CppSystem IEnumerable, not the CLR interface.
                // Close only the skipped side previews; the head/voice preview belongs to another state.
                if (SelectSideNodesAccessor?.GetValue(sideSelectionState) is Il2CppSystem.Collections.Generic.List<SideSelectionState.SelectSideNode> nodes)
                {
                    for (int index = 0; index < nodes.Count; index++)
                    {
                        PlayerProfilePreview preview = nodes[index]?.Preview;
                        if (preview != null)
                        {
                            preview.Close();
                            preview.gameObject.SetActive(false);
                        }
                    }
                }

                DefaultUIButton backButton = BackButtonField?.GetValue(screen) as DefaultUIButton;
                Button buttonComponent = backButton != null ? backButton.GetComponent<Button>() : null;
                if (buttonComponent != null)
                {
                    Navigation navigation = buttonComponent.navigation;
                    navigation.mode = Navigation.Mode.None;
                    buttonComponent.navigation = navigation;
                }
            }
            catch (Exception ex)
            {
                pitFireTeam.Log.LogError("[UI] Failed to hide skipped side selection visuals.");
                pitFireTeam.Log.LogError(ex);
            }
        }

        private static void ConfigureHeadSelectionUi(EftAccountSideSelectionScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            try
            {
                HeadSelectionState headSelectionState = HeadSelectionStateAccessor?.GetValue(screen) as HeadSelectionState;
                DefaultUIButton nextButton = NextButtonAccessor?.GetValue(screen) as DefaultUIButton;
                if (headSelectionState == null || nextButton == null)
                {
                    return;
                }

                PrepareNicknameField(headSelectionState);
                headSelectionState.StateReady = true;
                nextButton.gameObject.SetActive(true);
                nextButton.SetRawText(GetLocalizedSocialUi("AddTeammateConfirm"), nextButton.HeaderSize);
                bool nicknameValid = headSelectionState._nicknameField != null &&
                    headSelectionState._nicknameField.ValidationError(GetNicknameText(headSelectionState)) == ENicknameError.ValidNickname;
                nextButton.Interactable = !submitInProgress && nicknameValid;

                WireFlowButtons(screen, nextButton);
            }
            catch (Exception ex)
            {
                pitFireTeam.Log.LogError("[UI] Failed to configure head selection UI for add teammate flow.");
                pitFireTeam.Log.LogError(ex);
            }
        }

        private static void PrepareNicknameField(HeadSelectionState headSelectionState)
        {
            if (nicknamePrepared || headSelectionState == null)
            {
                return;
            }

            TMP_InputField inputField = headSelectionState._nicknameField != null
                ? NicknameInputAccessor?.GetValue(headSelectionState._nicknameField) as TMP_InputField
                : null;

            if (inputField != null)
            {
                inputField.interactable = true;
                inputField.readOnly = false;
                // Native NicknameField.Init already clears the reserved nickname.
                // Do not erase a first edit if preparation runs from its callback.
                inputField.Select();
                inputField.ActivateInputField();
            }

            headSelectionState._nicknameField?.ShowNicknameError(ENicknameError.ValidNickname, false);
            nicknamePrepared = true;
        }

        private static string GetNicknameText(HeadSelectionState headSelectionState)
        {
            if (headSelectionState?._nicknameField == null)
            {
                return string.Empty;
            }

            TMP_InputField inputField = NicknameInputAccessor?.GetValue(headSelectionState._nicknameField) as TMP_InputField;
            return inputField?.text ?? headSelectionState._profileData?.Nickname ?? string.Empty;
        }

        private static void WireFlowButtons(EftAccountSideSelectionScreen screen, DefaultUIButton nextButton)
        {
            Button next = nextButton.GetComponent<Button>();
            if (next != null && !ReferenceEquals(wiredNextButton, next))
            {
                if (wiredNextButton != null && nextButtonAction != null)
                {
                    wiredNextButton.onClick.RemoveListener(nextButtonAction);
                }

                next.onClick.RemoveAllListeners();
                nextButtonAction = (UnityAction)(System.Action)OnNextButtonPressed;
                next.onClick.AddListener(nextButtonAction);
                wiredNextButton = next;
            }

            DefaultUIButton backButton = BackButtonField?.GetValue(screen) as DefaultUIButton;
            Button back = backButton != null ? backButton.GetComponent<Button>() : null;
            if (back != null && !ReferenceEquals(wiredBackButton, back))
            {
                if (wiredBackButton != null && backButtonAction != null)
                {
                    wiredBackButton.onClick.RemoveListener(backButtonAction);
                }

                back.onClick.RemoveAllListeners();
                backButtonAction = (UnityAction)(System.Action)ReturnToMainScreen;
                back.onClick.AddListener(backButtonAction);
                wiredBackButton = back;
            }

        }

        private static void OnNextButtonPressed()
        {
            TryCompleteFromCurrentScreen();
        }

        private static IEnumerator SubmitTeammateCoroutine(FriendlyTeammateCreateRequest payload)
        {
            // Unity starts and advances this coroutine on its owner thread; serialize the managed DTO
            // before dispatching the network request to a worker because SptJsonBoundary enforces that boundary.
            string requestJson = SerializeRequest(payload);
            Task<string> requestTask = Task.Run(() => RequestHandler.PostJson(
                "/singleplayer/pitfireteam/teammate/create",
                requestJson));

            while (!requestTask.IsCompleted)
            {
                yield return null;
            }

            submitCoroutine = null;

            if (requestTask.IsFaulted)
            {
                submitInProgress = false;
                pitFireTeam.Log.LogError("[UI] Failed to create teammate in backend.");
                pitFireTeam.Log.LogError(requestTask.Exception);
                ShowToast(GetLocalizedSocialUi("AddTeammateCreateFailed"));
                RefreshSubmitButton();
                yield break;
            }

            try
            {
                HandleCreateTeammateResponse(payload, requestTask.Result);
            }
            catch (Exception ex)
            {
                submitInProgress = false;
                pitFireTeam.Log.LogError("[UI] Failed to process teammate create response.");
                pitFireTeam.Log.LogError(ex);
                ShowToast(GetLocalizedSocialUi("AddTeammateCreateFailed"));
                RefreshSubmitButton();
            }
        }

        private static void HandleCreateTeammateResponse(FriendlyTeammateCreateRequest payload, string responseJson)
        {
            FriendlyTeammateBackendResponse response = JsonConvert.DeserializeObject<FriendlyTeammateBackendResponse>(responseJson);
            if (response == null || response.err != 0)
            {
                string backendError = response?.errmsg;
                throw new Exception(string.IsNullOrEmpty(backendError) ? GetLocalizedSocialUi("AddTeammateCreateFailed") : backendError);
            }

            Logger.LogInfo($"[UI] Add teammate created in backend: {responseJson}");
            pitFireTeam.Instance.StartCoroutine(ShowSuccessAndReturn(payload.nickname));
        }

        private static IEnumerator ShowSuccessAndReturn(string nickname)
        {
            string messageTemplate = GetLocalizedSocialUi("AddTeammateInProgress");
            ShowToast(string.Format(messageTemplate, nickname ?? string.Empty));
            SocialNetworkClassPatch.RefreshFriendsList();
            Components.SquadControlMenuUi.RequestRosterRefreshOnNextInject();
            yield return null;
            ReturnToMainScreen();
        }

        private static void InvokePendingReturnAction()
        {
            Action callback = pendingReturnAction;
            pendingReturnAction = null;
            if (callback == null)
            {
                return;
            }

            try
            {
                // Returning from the add-teammate flow should reopen squad side-selection
                // on the first back press. Clear squad-mode guard before invoking callback.
                SquadSideSelectionFlow.Deactivate("return-from-add-teammate");
                callback();
            }
            catch (Exception ex)
            {
                pitFireTeam.Log.LogError("[UI] Failed to invoke add teammate return action.");
                pitFireTeam.Log.LogError(ex);
            }
        }

        private static string SerializeRequest(FriendlyTeammateCreateRequest payload)
        {
            return pitTeam.Compat.SptJsonBoundary.Serialize(payload);
        }

        private sealed class FriendlyTeammateCreateRequest
        {
            public string nickname;
            public string voice;
            public string head;
        }

        private sealed class FriendlyTeammateBackendResponse
        {
            public int err;
            public string errmsg;
            public object data;
        }
    }
}
