using DrakiaXYZ.BigBrain.Brains;
using EFT;
using System;

using Il2CppInterop.Runtime.Attributes;

namespace DrakiaXYZ.BigBrain.Internal
{
    // The managed wrapper is tracked by pointer; EFT uses only the native base.
    internal class CustomLogicWrapper : StandByNode
    {
        private CustomLogic customLogic;
        private BotOwner bot;
        private bool started;
        private int updateNodePrefixCalls;
        private int updateNodeByMainCalls;
        private int startCalls;
        private int stopCalls;
        private IntPtr lastNativeLayerPointer;
        private string lastManagedLayerActionType;
        private string lastStage;
        private string firstError;



        public CustomLogicWrapper(Type logicType, BotOwner owner)
            : base(owner)
        {


            bot = owner;
            customLogic = (CustomLogic)Activator.CreateInstance(logicType, new object[] { owner });
            NativeBrainRegistry.RegisterLogic(this, owner);
        }

        public override void UpdateNodeByMain(CoreActionResultParams data)
        {
            updateNodeByMainCalls++;
            try
            {
                var layerPointer = bot?.Brain?.BaseBrain?.CurLayerInfo?.Pointer ?? IntPtr.Zero;
                lastNativeLayerPointer = layerPointer;
                var layer = NativeBrainRegistry.ResolveLayer(layerPointer);
                if (layer == null)
                    throw new InvalidOperationException("Custom logic executed without its custom layer.");
                var currentAction = layer.CustomLayer().CurrentAction;
                lastManagedLayerActionType = currentAction?.GetType().FullName;
                if (!started) Start();
                customLogic.Update(currentAction?.Data);
                lastStage = "updated";
            }
            catch (Exception error)
            {
                RecordFailure("update-node-by-main", error);
                throw;
            }
        }

        public void Start()
        {
            startCalls++;
            if (started) { lastStage = "start-already"; return; }
            started = true;
            try
            {
                customLogic.Start();
                lastStage = "started";
            }
            catch (Exception error)
            {
                RecordFailure("start", error);
                throw;
            }
        }

        public void Stop()
        {
            stopCalls++;
            if (!started) { lastStage = "stop-not-started"; return; }
            started = false;
            try
            {
                customLogic.Stop();
                lastStage = "stopped";
            }
            catch (Exception error)
            {
                RecordFailure("stop", error);
                throw;
            }
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }

        internal IntPtr OwnerPointer => bot?.Pointer ?? IntPtr.Zero;
        internal void RecordNativeUpdatePrefix() { try { updateNodePrefixCalls++; lastStage = "standby-node-update-prefix"; } catch { } }
        internal object GetDiagnosticSnapshot() { return new { logicPointer = Pointer.ToString(), botPointer = OwnerPointer.ToString(), logicType = customLogic?.GetType().FullName, standbyNodeUpdatePrefixCalls = updateNodePrefixCalls, updateNodeByMainCalls = updateNodeByMainCalls, startCalls = startCalls, stopCalls = stopCalls, lastObservedNativeLayerPointer = lastNativeLayerPointer.ToString(), currentManagedLayerActionType = lastManagedLayerActionType, lastStage = lastStage, firstError = firstError }; }
        private void RecordFailure(string stage, Exception error) { try { lastStage = stage; if (firstError == null) firstError = error == null ? "unknown" : error.ToString(); } catch { } }
        [HideFromIl2Cpp]
        internal CustomLogic CustomLogic() => customLogic;
    }
}
