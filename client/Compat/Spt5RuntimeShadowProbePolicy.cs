using System;

namespace pitTeam.Compat
{
    public static class Spt5RuntimeShadowProbePolicy
    {
        public static void RequireEnabled(string probeRoot)
        {
            if (string.IsNullOrWhiteSpace(probeRoot))
                throw new InvalidOperationException("shadow-probe-root-required");
        }
    }
}
