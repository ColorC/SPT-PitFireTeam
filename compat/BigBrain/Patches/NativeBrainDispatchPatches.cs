using DrakiaXYZ.BigBrain.Internal;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using System;
using System.Reflection;

namespace DrakiaXYZ.BigBrain.Patches
{
    internal static class NativeBrainDispatchPatches
    {
        internal sealed class StandByGetDecisionPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(StandByLogicLayer), "GetDecision", Type.EmptyTypes);

            [PatchPrefix]
            private static bool PatchPrefix(StandByLogicLayer __instance, ref AICoreActionResult<BotLogicDecision, CoreActionResultParams> __result)
            {
                CustomLayerWrapper wrapper = NativeBrainRegistry.ResolveLayer(__instance.Pointer); NativeBrainRegistry.RecordDispatchPrefix(__instance.Pointer, true, wrapper != null);
                if (wrapper == null) return true;
                try { __result = wrapper.GetDecision(); NativeBrainRegistry.RecordDecision(__instance.Pointer, __result); } catch (Exception error) { NativeBrainRegistry.RecordDispatchError(__instance.Pointer, error); throw; }
                return false;
            }
        }

        internal sealed class StandByShallUseNowPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(StandByLogicLayer), "ShallUseNow", Type.EmptyTypes);

            [PatchPrefix]
            private static bool PatchPrefix(StandByLogicLayer __instance, ref bool __result)
            {
                CustomLayerWrapper wrapper = NativeBrainRegistry.ResolveLayer(__instance.Pointer); NativeBrainRegistry.RecordDispatchPrefix(__instance.Pointer, false, wrapper != null);
                if (wrapper == null) return true;
                try { __result = wrapper.ShallUseNow(); NativeBrainRegistry.RecordIsActive(__instance.Pointer, __result); } catch (Exception error) { NativeBrainRegistry.RecordDispatchError(__instance.Pointer, error); throw; }
                return false;
            }
        }

        internal sealed class StandByNamePatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(StandByLogicLayer), "Name", Type.EmptyTypes);

            [PatchPrefix]
            private static bool PatchPrefix(StandByLogicLayer __instance, ref string __result)
            {
                CustomLayerWrapper wrapper = NativeBrainRegistry.ResolveLayer(__instance.Pointer);
                if (wrapper == null) return true;
                __result = wrapper.Name();
                return false;
            }
        }

        internal sealed class BaseLayerShallEndPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() => AccessTools.Method(
                typeof(BaseLogicLayerSimple),
                "ShallEndCurrentDecision",
                new[] { typeof(AICoreActionResult<BotLogicDecision, CoreActionResultParams>) });

            [PatchPrefix]
                        private static bool PatchPrefix(
                BaseLogicLayerSimple __instance,
                AICoreActionResult<BotLogicDecision, CoreActionResultParams> curDecision,
                ref AICoreActionEnd __result)
            {
                IntPtr receiverPointer = __instance.Pointer;
                CustomLayerWrapper wrapper = NativeBrainRegistry.ResolveLayer(receiverPointer);
                if (wrapper == null)
                {
                    RecordObservation(receiverPointer, false, curDecision, null, null, null);
                    return true;
                }

                __result = wrapper.ShallEndCurrentDecision(curDecision);
                bool? shouldEnd = null;
                string endReason = null;
                try
                {
                    shouldEnd = __result.Value;
                    endReason = __result.ToString();
                }
                catch { }
                RecordObservation(receiverPointer, true, curDecision, wrapper, shouldEnd, endReason);
                return false;
            }

            private static void RecordObservation(
                IntPtr receiverPointer,
                bool hit,
                AICoreActionResult<BotLogicDecision, CoreActionResultParams> curDecision,
                CustomLayerWrapper wrapper,
                bool? shouldEnd,
                string endReason)
            {
                try
                {
                    int? decisionAction = null;
                    string decisionReason = null;
                    string wrapperType = null;
                    string wrapperActionType = null;
                    int? wrapperActionId = null;
                    try
                    {
                        decisionAction = Convert.ToInt32(curDecision.Action);
                        decisionReason = curDecision.Reason;
                    }
                    catch { }
                    try
                    {
                        if (wrapper != null)
                        {
                            wrapperType = wrapper.GetType().FullName;
                            var action = wrapper.CustomLayer()?.CurrentAction;
                            wrapperActionType = action?.Type?.FullName;
                            if (action?.Type != null &&
                                BrainManager.Instance.CustomLogics.TryGetValue(action.Type, out int mappedId))
                                wrapperActionId = mappedId;
                        }
                    }
                    catch { }
                    NativeBrainRegistry.RecordShallEndObservation(receiverPointer, hit, decisionAction, decisionReason, wrapperType, wrapperActionType, wrapperActionId, shouldEnd, endReason);
                }
                catch { }
            }
        }

        internal sealed class StandByNodeUpdatePatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() => AccessTools.Method(
                typeof(StandByNode),
                "UpdateNodeByBrain",
                new[] { typeof(CoreActionResultParams) });

            [PatchPrefix]
                        private static bool PatchPrefix(StandByNode __instance, CoreActionResultParams data)
            {
                CustomLogicWrapper wrapper = NativeBrainRegistry.ResolveLogic(__instance.Pointer);
                if (wrapper == null) return true;
                wrapper.RecordNativeUpdatePrefix();
                wrapper.UpdateNodeByMain(data);
                return false;
            }
        }

        internal sealed class StandartBrainDisposePatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(StandartBotBrain), "Dispose", Type.EmptyTypes);

            [PatchPrefix]
            private static void PatchPrefix(StandartBotBrain __instance)
            {
                try
                {
                    NativeBrainRegistry.StopAndClear(__instance?._owner);
                }
                catch (Exception ex)
                {
                    BigBrainPlugin.BigBrainLogger.LogError("Failed clearing custom brain wrappers during disposal: " + ex);
                }
            }
        }
    }
}
