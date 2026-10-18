using SPTushonka.Reflection.Patching;
using DrakiaXYZ.BigBrain.Internal;
using HarmonyLib;
using System;
using System.Reflection;

namespace DrakiaXYZ.BigBrain.Patches
{
    internal class BotBaseBrainActivateLayerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method(typeof(AICoreStrategy<BotLogicDecision>), "InsertLayerToActive",
                new[] { typeof(AICoreLayer<BotLogicDecision>), typeof(int) });

        [PatchPostfix]
        public static void PatchPostfix(AICoreStrategy<BotLogicDecision> __instance,
            AICoreLayer<BotLogicDecision> layer)
        {
            if (NativeBrainRegistry.ResolveLayer(layer?.Pointer) == null) return;
            // Native activation maintains the active-ID set and events. Only reorder its resulting list.
            var layers = __instance._activeLayers;
            for (int i = layers.Count - 1; i >= 0; i--)
                if (layers[i].Pointer == layer.Pointer) layers.RemoveAt(i);
            int position = 0;
            while (position < layers.Count && layers[position].Priority > layer.Priority) position++;
            layers.Insert(position, layer);
        }
    }
}
