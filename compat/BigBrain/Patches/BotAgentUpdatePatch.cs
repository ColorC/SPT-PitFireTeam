using SPTushonka.Reflection.Patching;
using DrakiaXYZ.BigBrain.Internal;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;

namespace DrakiaXYZ.BigBrain.Patches
{
    /**
     * Patch the bot agent update method so we can trigger a Start() method on custom logic actions
     **/
    internal class BotAgentUpdatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(AICoreAgent<BotLogicDecision>), "Update");
        }

        [PatchPrefix]
        public static bool PatchPrefix(AICoreAgent<BotLogicDecision> __instance)
        {
            IntPtr agentPointer = IntPtr.Zero;
            try { agentPointer = __instance.Pointer; } catch { }
            IntPtr layerPointer = IntPtr.Zero;
            try { if (__instance._strategy.CurLayerInfo != null) layerPointer = __instance._strategy.CurLayerInfo.Pointer; } catch { }
            NativeBrainRegistry.RecordAgentPrefix(agentPointer, layerPointer);
            try {
                // Update the brain, this is instead of method_10 in the original code
                __instance._strategy.ManualUpdate();

                // Call the brain update
                AICoreActionResult<BotLogicDecision, CoreActionResultParams>? result = __instance._strategy.Update(__instance._lastResult);
                NativeBrainRegistry.RecordStrategy(agentPointer, __instance._lastResult, result.HasValue ? (object)result.Value : null);
                if (result != null)
                {
                    // If an instance of our action doesn't exist in our dict, add it
                    BotLogicDecision action = result.Value.Action;
                    AICoreNode nodeInstance;
                    if (!__instance._nodesDictionary.TryGetValue(action, out nodeInstance))
                    {
                        NativeBrainRegistry.RecordLazy(agentPointer, action);
                        nodeInstance = __instance._lazyGetter(action);

                        if (nodeInstance != null)
                        {
                            __instance._nodesDictionary.Add(action, nodeInstance);
                        }
                    }

                    if (nodeInstance != null)
                    {
                        // If we're switching to a new action, call Start() on the new logic
                        CustomLogicWrapper customLogic = null;
                        IntPtr nodePointer = IntPtr.Zero;
                        try { nodePointer = nodeInstance.Pointer; customLogic = NativeBrainRegistry.ResolveLogic(nodePointer); }
                        catch (Exception diagnosticError) { NativeBrainRegistry.RecordAgentError(agentPointer, "resolve-logic", diagnosticError); }
                        NativeBrainRegistry.RecordNode(agentPointer, nodePointer, customLogic != null);
                        if (__instance._lastResult.Action != result.Value.Action && customLogic != null)
                        {
                            NativeBrainRegistry.RecordAgentStage(agentPointer, "start");
                            customLogic.Start();
                        }

                        NativeBrainRegistry.RecordAgentStage(agentPointer, "update");
                        nodeInstance.UpdateNodeByMain(__instance._lastResult.Data);
                    }

                    __instance._lastResult = result.Value;
                    NativeBrainRegistry.RecordWriteback(agentPointer, __instance._lastResult);
                }

                return false;

            }
            catch (Exception ex)
            {
                NativeBrainRegistry.RecordAgentError(agentPointer, "update-exception", ex);
#if DEBUG
                Logger.LogError(ex);
#endif
                throw;
            }
        }
    }
}
