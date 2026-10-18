namespace pitTeam.Compat
{
    internal static class Spt5QaHealthMaintenancePolicy
    {
        public static bool ShouldInvokeFullHealthMaintenance(bool requested, bool protectionGuardSatisfied)
            => requested && protectionGuardSatisfied;

        public static string? SkipReason(bool requested, bool protectionGuardSatisfied)
            => !requested && protectionGuardSatisfied ? "full-health-maintenance-disabled-by-probe" : null;
    }
}
