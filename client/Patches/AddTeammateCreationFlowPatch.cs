using EFT.UI;
using EFT.UI.Screens;
using pitTeam.Modules;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using System;
using System.Reflection;

namespace pitTeam.Patches
{
    internal class AddTeammateSideSelectionStatePreparePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SideSelectionState), nameof(SideSelectionState.PrepareAsync), Type.EmptyTypes);
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref Il2CppSystem.Threading.Tasks.Task __result)
        {
            if (!AddTeammateCreationFlow.IsActive || !AddTeammateCreationFlow.SuppressSkippedSideSelectionModelView)
                return true;
            __result = Il2CppSystem.Threading.Tasks.Task.CompletedTask;
            return false;
        }
    }

    internal class AddTeammateSideSelectionStateShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SideSelectionState), nameof(SideSelectionState.ShowState), Type.EmptyTypes);
        }

        [PatchPrefix]
        private static bool PatchPrefix(SideSelectionState __instance, ref Il2CppSystem.Threading.Tasks.Task __result)
        {
            if (!AddTeammateCreationFlow.IsActive || !AddTeammateCreationFlow.SuppressSkippedSideSelectionModelView)
                return true;

            // A teammate already inherits the player's side. The native side
            // previews are never displayed and can fault before OnShow fires,
            // preventing the existing head/voice transition from being scheduled.
            if (__instance._stateCanvasGroup != null)
            {
                __instance._stateCanvasGroup.alpha = 0f;
                __instance._stateCanvasGroup.gameObject.SetActive(false);
            }
            __instance.StateReady = true;
            __result = Il2CppSystem.Threading.Tasks.Task.CompletedTask;
            return false;
        }
    }

    internal class AddTeammateBackButtonPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            Type targetType = typeof(EftAccountSideSelectionScreen).BaseType;
            return AccessTools.Method(targetType, "BackButtonPressedHandler");
        }

        [PatchPrefix]
        private static bool PatchPrefix(object __instance)
        {
            if (!AddTeammateCreationFlow.IsActiveForController(EFT.UI.Screens.EftScreenManager.Instance.CurrentScreenController))
            {
                return true;
            }

            AddTeammateCreationFlow.ReturnToMainScreen();
            return false;
        }
    }

    internal class AddTeammateSideSelectionStateClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SideSelectionState), nameof(SideSelectionState.Close));
        }

        [PatchPrefix]
        private static bool PatchPrefix(SideSelectionState __instance)
        {
            if (!AddTeammateCreationFlow.IsActive)
            {
                return true;
            }

            __instance._stateReady = false;
            __instance._compositeDisposable?.Dispose();
            if (__instance._stateCanvasGroup != null)
            {
                __instance._stateCanvasGroup.alpha = 0f;
                __instance._stateCanvasGroup.gameObject.SetActive(false);
            }

            return false;
        }
    }
}
