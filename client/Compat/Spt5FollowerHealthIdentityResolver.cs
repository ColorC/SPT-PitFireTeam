using System;

namespace pitTeam.Compat
{
    internal static class Spt5FollowerHealthIdentityResolver
    {
        internal static bool TryResolve(string nativeProfileId, string snapshotProfileId, string followerAid, int? snapshotAid, string inventoryContextProfileId, string inventoryContextAid, out string resolvedProfileId, out string resolution, out string error)
        {
            resolvedProfileId = string.Empty; resolution = "unavailable"; error = string.Empty;
            if (!IsMongoProfileId(snapshotProfileId)) { error = "snapshot-profile-id-missing-or-invalid"; return false; }
            if (!int.TryParse(followerAid, out int parsedAid) || !snapshotAid.HasValue || snapshotAid.Value != parsedAid) { error = "snapshot-aid-mismatch"; return false; }
            if (string.Equals(snapshotProfileId, inventoryContextProfileId, StringComparison.Ordinal) && (!int.TryParse(inventoryContextAid, out int contextAid) || !int.TryParse(followerAid, out int followerAccountId) || contextAid != followerAccountId)) { error = "inventory-snapshot-aid-mismatch"; return false; }
            if (!string.IsNullOrWhiteSpace(nativeProfileId))
            {
                if (!IsMongoProfileId(nativeProfileId)) { error = "native-profile-id-invalid"; return false; }
                if (!string.Equals(nativeProfileId, snapshotProfileId, StringComparison.Ordinal)) { error = "explicit-profile-id-mismatch"; return false; }
                resolvedProfileId = nativeProfileId; resolution = "native-profile-id"; return true;
            }
            if (!IsMongoProfileId(inventoryContextProfileId)) { error = "snapshot-id-not-distinct-from-inventory-context"; return false; }
            resolvedProfileId = snapshotProfileId; resolution = "snapshot-aid-fallback"; return true;
        }
        private static bool IsMongoProfileId(string value)
        {
            if (value == null || value.Length != 24) return false;
            for (int i = 0; i < value.Length; i++) if (!Uri.IsHexDigit(value[i])) return false;
            return true;
        }
    }
}
