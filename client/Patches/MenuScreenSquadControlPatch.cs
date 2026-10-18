using EFT;
using EFT.UI;
using pitTeam.Components;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using System;
using System.Reflection;

namespace pitTeam.Patches
{
    internal class MenuScreenShowSquadControlPatch : ModulePatch
    {
        private static readonly pitTeam.Compat.NativeUiMemberAccessor PlayerButtonAccessor = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(MenuScreen), "_playerButton", typeof(DefaultUIButton), false, false);
        private static readonly pitTeam.Compat.NativeUiMemberAccessor TradeButtonAccessor = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(MenuScreen), "_tradeButton", typeof(DefaultUIButton), false, false);
        private static readonly pitTeam.Compat.NativeUiMemberAccessor HideScreenButtonAccessor = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(MenuScreen), "_hideScreenButton", typeof(DefaultUIButton), false, false);

        protected override MethodBase GetTargetMethod()
        {
                        foreach (MethodBase method in AccessTools.GetDeclaredMethods(typeof(MenuScreen)))
            {
                if (method.Name != "Show") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 5
                    && parameters[0].ParameterType == typeof(EFT.UI.Matchmaker.MatchmakerPlayersController)
                    && parameters[3].ParameterType == typeof(Profile))
                {
                    return method;
                }
            }

            return null;
        }

        [PatchPostfix]
        private static void PatchPostfix(MenuScreen __instance)
        {
            DefaultUIButton playerButton = PlayerButtonAccessor.GetValue(__instance) as DefaultUIButton;
            DefaultUIButton tradeButton = TradeButtonAccessor.GetValue(__instance) as DefaultUIButton;
            DefaultUIButton hideScreenButton = HideScreenButtonAccessor.GetValue(__instance) as DefaultUIButton;
            if (playerButton == null)
            {
                return;
            }

            SquadControlMenuUi.GetOrCreate(__instance).Initialize(__instance, playerButton, tradeButton, hideScreenButton);
        }
    }

    internal class MenuScreenReconnectVisibilitySquadControlPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MenuScreen), nameof(MenuScreen.ChangeToReconnectionAvailable));
        }

        [PatchPostfix]
        private static void PatchPostfix(MenuScreen __instance)
        {
            __instance.GetComponentViaIl2CppType<SquadControlMenuUi>()?.SyncButtonVisibility();
        }
    }

    internal class MenuScreenMinimizedVisibilitySquadControlPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MenuScreen), nameof(MenuScreen.ChangeScreenInGameStatus));
        }

        [PatchPostfix]
        private static void PatchPostfix(MenuScreen __instance)
        {
            __instance.GetComponentViaIl2CppType<SquadControlMenuUi>()?.SyncButtonVisibility();
        }
    }
}
