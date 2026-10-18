using System.Collections.Generic;

namespace BepInEx.Bootstrap
{
    internal static class Chainloader
    {
        public static readonly Dictionary<string, BepInEx.PluginInfo> PluginInfos =
            new Dictionary<string, BepInEx.PluginInfo>();
    }
}
