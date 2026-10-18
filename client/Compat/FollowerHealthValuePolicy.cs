namespace pitTeam.Compat
{
    internal static class FollowerHealthValuePolicy
    {
        internal static bool TryResolve(double? current, double? minimum, double? maximum, out float currentValue, out float? minimumValue, out float maximumValue, out string missingFields)
        {
            currentValue = 0f; minimumValue = null; maximumValue = 0f;
            var missing = new System.Collections.Generic.List<string>();
            if (!current.HasValue) missing.Add("Current");
            if (!maximum.HasValue) missing.Add("Maximum");
            if (missing.Count != 0)
            {
                missingFields = string.Join(",", missing);
                return false;
            }
            currentValue = (float)current.Value;
            maximumValue = (float)maximum.Value;
            if (minimum.HasValue) minimumValue = (float)minimum.Value;
            missingFields = string.Empty;
            return true;
        }
    }
}