using System;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using pitTeam.Patches;

namespace pitTeam.Compat
{
    internal static class Spt5FollowerHealthControllerFactory
    {
        internal static bool TryCreate(OtherPlayerProfile follower, InventoryController inventory, FollowerHealthSnapshot snapshot, out Profile.HealthInfo healthInfo, out IHealthController controller, out string error, out string diagnostic)
        {
            healthInfo = null; controller = null; error = string.Empty; diagnostic = "inventory-context-unavailable";
            if (follower == null || inventory?.Profile == null || snapshot == null) { error = "follower profile, inventory profile, or follower health snapshot is missing"; return false; }
            string nativeProfileId = follower.ProfileId ?? string.Empty;
            string inventoryContextProfileId = inventory.Profile.ProfileId ?? string.Empty;
            Profile inventoryOwner = Il2CppBoundary.CastNativeOrNull<Profile>(inventory.Profile);
            string inventoryContextAid = inventoryOwner?.AccountId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(inventoryContextAid)) inventoryContextAid = Il2CppBoundary.CastNativeOrNull<OtherPlayerProfile>(inventory.Profile)?.AccountId ?? string.Empty;
            bool inventoryContextMatchesFollower = string.Equals(inventoryContextProfileId, nativeProfileId, StringComparison.Ordinal);
            if (!Spt5FollowerHealthIdentityResolver.TryResolve(nativeProfileId, snapshot.ProfileId, follower.AccountId, snapshot.Aid, inventoryContextProfileId, inventoryContextAid, out string id, out string identityResolution, out error))
            {
                diagnostic = "followerProfileId=" + DescribeIdentity(nativeProfileId) + "; snapshotProfileId=" + DescribeIdentity(snapshot.ProfileId) + "; followerAid=" + DescribeIdentity(follower.AccountId) + "; snapshotAid=" + (snapshot.Aid?.ToString() ?? "<unavailable>") + "; inventoryContextProfileId=" + DescribeIdentity(inventoryContextProfileId) + "; inventoryContextAid=" + DescribeIdentity(inventoryContextAid) + "; identityResolution=" + identityResolution;
                return false;
            }
            diagnostic = "followerProfileId=" + DescribeIdentity(id) + "; nativeProfileId=" + DescribeIdentity(nativeProfileId) + "; snapshotProfileId=" + DescribeIdentity(snapshot.ProfileId) + "; followerAid=" + DescribeIdentity(follower.AccountId) + "; snapshotAid=" + (snapshot.Aid?.ToString() ?? "<unavailable>") + "; inventoryContextProfileId=" + DescribeIdentity(inventoryContextProfileId) + "; inventoryContextAid=" + DescribeIdentity(inventoryContextAid) + "; identityResolution=" + identityResolution + "; inventoryContextMatchesFollower=" + inventoryContextMatchesFollower;
            if (follower.Skills == null || snapshot.BodyParts == null || snapshot.BodyParts.Count == 0) { error = "follower skills or health body-part snapshot is missing"; return false; }
            try
            {
                var nativeHealth = new Profile.HealthInfo();
                var parts = new Il2CppSystem.Collections.Generic.Dictionary<EBodyPart, Profile.HealthInfo.BodyPartInfo>();
                foreach (var pair in snapshot.BodyParts)
                {
                    if (!Enum.TryParse(pair.Key, true, out EBodyPart part) || pair.Value == null || !TryBuildValue(pair.Value, "BodyParts." + pair.Key, out var value, out error)) { if (string.IsNullOrEmpty(error)) error = "follower health contains an unknown body-part key"; return false; }
                    parts.Add(part, new Profile.HealthInfo.BodyPartInfo { Health = value });
                }
                if (!TryBuildValue(snapshot.Energy, "Energy", out var energy, out error) || !TryBuildValue(snapshot.Hydration, "Hydration", out var hydration, out error)) return false;
                nativeHealth.BodyParts = parts; nativeHealth.Energy = energy; nativeHealth.Hydration = hydration;
                if (snapshot.Temperature != null) { if (!TryBuildValue(snapshot.Temperature, "Temperature", out var temperature, out error)) return false; nativeHealth.Temperature = temperature; }
                if (snapshot.Poison != null) { if (!TryBuildValue(snapshot.Poison, "Poison", out var poison, out error)) return false; nativeHealth.Poison = poison; }
                var nativeController = new OfflineHealthController(nativeHealth, inventory, follower.Skills, false);
                controller = nativeController as IHealthController;
                if (controller == null) { error = "native OfflineHealthController interface cast failed"; return false; }
                healthInfo = nativeHealth; return true;
            }
            catch (Exception ex) { error = ex.GetType().FullName + ": " + ex.Message; healthInfo = null; controller = null; return false; }
        }

        private static string DescribeIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "<unavailable>";
            string normalized = value.Replace("\r", " ").Replace("\n", " ").Replace(";", "%3B");
            return normalized.Length <= 128 ? normalized : normalized.Substring(0, 128) + "...";
        }

        private static bool TryBuildValue(FollowerHealthValue source, string path, out Profile.HealthInfo.ValueInfo value, out string error)
        {
            value = null; error = string.Empty;
            if (!FollowerHealthValuePolicy.TryResolve(source?.Current, source?.Minimum, source?.Maximum, out float current, out float? minimum, out float maximum, out string missingFields))
            {
                error = "follower health value incomplete at " + path + ": missing " + missingFields;
                return false;
            }
            value = new Profile.HealthInfo.ValueInfo { Current = current, Maximum = maximum };
            if (minimum.HasValue) value.Minimum = minimum.Value;
            return true;
        }
    }
}
