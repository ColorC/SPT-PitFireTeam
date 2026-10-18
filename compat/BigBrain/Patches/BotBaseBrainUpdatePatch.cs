using SPTushonka.Reflection.Patching;
using DrakiaXYZ.BigBrain.Internal;
using HarmonyLib;
using System.Reflection;

namespace DrakiaXYZ.BigBrain.Patches
{
    internal class BotBaseBrainUpdatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method(typeof(AICoreStrategy<BotLogicDecision>), "Update",
                new[] { typeof(AICoreActionResult<BotLogicDecision, CoreActionResultParams>) });

        [PatchPrefix]
        public static void Prefix(AICoreStrategy<BotLogicDecision> __instance,
            out AICoreLayer<BotLogicDecision> __state) => __state = __instance.TryCast<BaseBrain>()?.CurLayerInfo;

        [PatchPostfix]
        public static void Postfix(AICoreStrategy<BotLogicDecision> __instance,
            AICoreLayer<BotLogicDecision> __state)
        {
            var current = __instance.TryCast<BaseBrain>()?.CurLayerInfo;
            if (__state?.Pointer == current?.Pointer) return;
            NativeBrainRegistry.ResolveLayer(__state?.Pointer)?.Stop();
            NativeBrainRegistry.ResolveLayer(current?.Pointer)?.Start();
        }
    }
}
