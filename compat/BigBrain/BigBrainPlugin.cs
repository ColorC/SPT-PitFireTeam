using BepInEx;
using BepInEx.Unity.IL2CPP;
using DrakiaXYZ.BigBrain.Patches;
using DrakiaXYZ.BigBrain.VersionChecker;
using System;
using DrakiaXYZ.BigBrain.Internal;

namespace DrakiaXYZ.BigBrain
{
    [BepInPlugin("xyz.drakia.bigbrain", "DrakiaXYZ-BigBrain", "1.5.0")]
#if !DEBUG
    [BepInDependency("sptushonka.core", "5.0.0")]
#endif
    internal class BigBrainPlugin : BasePlugin
    {
        // This needs to be initialized because other mods could interact with BrainManager before this plugin has loaded
        internal static BepInEx.Logging.ManualLogSource BigBrainLogger { get; private set; } = new BepInEx.Logging.ManualLogSource("DrakiaXYZ-BigBrain");

        public override void Load()
        {
            Log.LogInfo("Loading: DrakiaXYZ-BigBrain");
            BigBrainLogger = Log;
            try
            {
                new BotBaseBrainActivatePatch().Enable();
                new NativeBrainDispatchPatches.StandByGetDecisionPatch().Enable();
                new NativeBrainDispatchPatches.StandByShallUseNowPatch().Enable();
                new NativeBrainDispatchPatches.StandByNamePatch().Enable();
                new NativeBrainDispatchPatches.BaseLayerShallEndPatch().Enable();
                new NativeBrainDispatchPatches.StandByNodeUpdatePatch().Enable();
                new NativeBrainDispatchPatches.StandartBrainDisposePatch().Enable();
                new BotBaseBrainUpdatePatch().Enable();
                new BotStandartBotBrainActivatePatch().Enable();
                new BotBrainCreateLogicNodePatch().Enable();
                new BotBaseBrainActivateLayerPatch().Enable();
            }
            catch (Exception ex)
            {
                Log.LogError($"{GetType().Name}: {ex}");
                throw;
            }

            Log.LogInfo("Completed: DrakiaXYZ-BigBrain");
        }
    }
}
