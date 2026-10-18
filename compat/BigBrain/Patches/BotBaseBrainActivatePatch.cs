using SPTushonka.Reflection.Patching;
using DrakiaXYZ.BigBrain.Brains;
using DrakiaXYZ.BigBrain.Internal;
using EFT;
using HarmonyLib;
using System;
using System.Reflection;

namespace DrakiaXYZ.BigBrain.Patches
{
    internal class BotBaseBrainActivatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method(typeof(AICoreStrategy<BotLogicDecision>), "Activate", new[] { typeof(AICoreAgentBase) });

        [PatchPrefix]
        public static void PatchPrefix(AICoreStrategy<BotLogicDecision> __instance)
        {
            var brain = __instance.TryCast<BaseBrain>();
            if (brain == null) return;
            BotOwner owner = brain._owner;
            foreach (BrainManager.LayerInfo info in BrainManager.Instance.CustomLayers.Values)
            {
                if (!info.AffectsBot(owner) || brain._layers.ContainsKey(info.customLayerId)) continue;
                var layer = new CustomLayerWrapper(info.customLayerType, owner, info.customLayerPriority);
                if (!brain.TryAddLayer(info.customLayerId, layer, true))
                    throw new InvalidOperationException("Could not register custom layer " + info.customLayerId);
            }
        }
    }
}
