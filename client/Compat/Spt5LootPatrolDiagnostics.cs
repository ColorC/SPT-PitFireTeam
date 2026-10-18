using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
namespace pitTeam.Compat
{
    internal sealed class Spt5LootPatrolFaultSample
    {
        public string? timestampUtc { get; set; }
        public string? exceptionType { get; set; }
        public string? exceptionMessage { get; set; }
        public string? exceptionDetails { get; set; }
        public bool exceptionDetailsTruncated { get; set; }
        public string? exceptionStackTrace { get; set; }
        public bool exceptionStackTraceTruncated { get; set; }
        public string? innerExceptionType { get; set; }
        public string? innerExceptionMessage { get; set; }
        public string? innerExceptionChain { get; set; }
        public string? layerProxyType { get; set; }
        public string? layerProxyTypeStatus { get; set; }
        public string? layerNativeType { get; set; }
        public string? layerNativeTypeStatus { get; set; }
        public string? ownerProxyType { get; set; }
        public string? ownerProxyTypeStatus { get; set; }
        public string? ownerNativeType { get; set; }
        public string? ownerNativeTypeStatus { get; set; }
        public string? layerPointer { get; set; }
        public string? ownerPointer { get; set; }
        public string? ownerProfileId { get; set; }
        public string? ownerProfileIdStatus { get; set; }
        public string? ownerAccountId { get; set; }
        public string? ownerAccountIdStatus { get; set; }
        public string? ownerAccountIdStage { get; set; }
        public string? targetIdentityStatus { get; set; }
        public string? targetIdentityStage { get; set; }
        public string? ownerGetterStage { get; set; }
        public string? ownerGetterError { get; set; }
        public string? fallbackDecisionKind { get; set; }
        public bool? fallbackSucceeded { get; set; }
        public string? fallbackError { get; set; }
        public bool? confirmedFollower { get; set; }
        public string? captureError { get; set; }
    }
    internal static class Spt5LootPatrolDiagnostics
    {
        const int Cap = 16, Limit = 512, DetailsLimit = 8192, StackLimit = 4096, InnerLimit = 2048;
        static readonly object Gate = new object();
        static readonly Queue<Spt5LootPatrolFaultSample> Q = new Queue<Spt5LootPatrolFaultSample>();
        static long total;
        static DateTimeOffset? first, recent, sampled;
        static bool Enabled() => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT"));
        static string? Cut(string? s, int limit = Limit) => s == null || s.Length <= limit ? s : s.Substring(0, limit);
        internal static void RecordException(Exception ex, Func<Spt5LootPatrolFaultSample> capture)
        {
            try
            {
                if (ex == null || capture == null || !Enabled()) return;
                var now = DateTimeOffset.UtcNow;
                bool add;
                lock (Gate)
                {
                    total++;
                    if (!first.HasValue) first = now;
                    recent = now;
                    add = !sampled.HasValue || now - sampled.Value >= TimeSpan.FromSeconds(1);
                    if (add) sampled = now;
                }
                if (!add) return;
                Spt5LootPatrolFaultSample x;
                try { x = capture() ?? new Spt5LootPatrolFaultSample(); }
                catch (Exception e) { x = new Spt5LootPatrolFaultSample { captureError = Cut(e.GetType().FullName + ": " + e.Message) }; }
                x.timestampUtc = now.ToString("o", CultureInfo.InvariantCulture);
                x.exceptionType = Cut(ex.GetType().FullName);
                x.exceptionMessage = Cut(ex.Message);
                string details = SafeExceptionText(ex);
                x.exceptionDetailsTruncated = details.Length > DetailsLimit;
                x.exceptionDetails = Cut(details, DetailsLimit);
                string? stack = SafeStack(ex);
                x.exceptionStackTraceTruncated = stack != null && stack.Length > StackLimit;
                x.exceptionStackTrace = Cut(stack, StackLimit);
                x.innerExceptionType = ex.InnerException == null ? null : Cut(ex.InnerException.GetType().FullName);
                x.innerExceptionMessage = ex.InnerException == null ? null : Cut(ex.InnerException.Message);
                x.innerExceptionChain = Cut(InnerChain(ex), InnerLimit);
                lock (Gate) { Q.Enqueue(x); while (Q.Count > Cap) Q.Dequeue(); }
            }
            catch { }
        }
        internal static Spt5LootPatrolFaultSample Capture(object? layer, object? owner, string stage, string? ownerError, string fallbackKind, bool? fallbackOk, string? fallbackError, Func<bool?> confirmed)
        {
            var x = new Spt5LootPatrolFaultSample
            {
                ownerGetterStage = stage,
                ownerGetterError = Cut(ownerError),
                fallbackDecisionKind = fallbackKind,
                fallbackSucceeded = fallbackOk,
                fallbackError = Cut(fallbackError),
                targetIdentityStatus = "unavailable",
                targetIdentityStage = "GetDecision finalizer does not expose a separate target identity"
            };
            try { x.layerProxyType = layer?.GetType().FullName; x.layerProxyTypeStatus = layer == null ? "unavailable" : "managed-proxy-type"; x.layerNativeType = null; x.layerNativeTypeStatus = "unavailable-no-verified-native-type-accessor"; x.layerPointer = Pointer(layer); }
            catch (Exception e) { x.captureError = Add(x.captureError, Err("layer", e)); }
            if (owner == null)
            {
                x.ownerProxyTypeStatus = "unavailable";
                x.ownerNativeTypeStatus = "unavailable-no-verified-native-type-accessor";
                x.ownerProfileIdStatus = "unavailable";
                x.ownerAccountIdStatus = "unavailable";
                x.ownerAccountIdStage = "owner-not-captured";
                return x;
            }
            try { x.ownerProxyType = owner.GetType().FullName; x.ownerProxyTypeStatus = "managed-proxy-type"; x.ownerNativeType = null; x.ownerNativeTypeStatus = "unavailable-no-verified-native-type-accessor"; }
            catch (Exception e) { x.captureError = Add(x.captureError, Err("owner-type", e)); }
            try { x.ownerPointer = Pointer(owner); }
            catch (Exception e) { x.captureError = Add(x.captureError, Err("owner-pointer", e)); }
            try
            {
                x.ownerProfileId = ReadString(owner, "ProfileId");
                x.ownerProfileIdStatus = string.IsNullOrWhiteSpace(x.ownerProfileId) ? "unavailable" : "available";
            }
            catch (Exception e) { x.ownerProfileIdStatus = "error"; x.captureError = Add(x.captureError, Err("owner-profile-id", e)); }
            try
            {
                object? profile = Read(owner, "Profile");
                
                x.ownerAccountId = ReadString(profile, "AccountId");
                x.ownerAccountIdStatus = string.IsNullOrWhiteSpace(x.ownerAccountId) ? "unavailable" : "available";
                x.ownerAccountIdStage = "owner.Profile.AccountId";
            }
            catch (Exception e) { x.ownerAccountIdStatus = "error"; x.ownerAccountIdStage = "owner.Profile.AccountId"; x.captureError = Add(x.captureError, Err("owner-account-id", e)); }
            try { x.confirmedFollower = confirmed == null ? null : confirmed(); }
            catch (Exception e) { x.captureError = Add(x.captureError, Err("confirmed-follower", e)); }
            return x;
        }
        static string? NativeType(object? o) => o?.GetType().FullName;
        static string? Pointer(object? o) { object? p = Read(o, "Pointer"); return p is IntPtr i ? i.ToString("X") : p?.ToString(); }
        static string? ReadString(object? o, string n) => Read(o, n) as string;
        static object? Read(object? o, string n)
        {
            if (o == null) return null;
            var p = o.GetType().GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return p?.GetValue(o);
        }
        static string SafeExceptionText(Exception ex) { try { return ex.ToString() ?? ex.GetType().FullName ?? "Exception"; } catch { return ex.GetType().FullName ?? "Exception"; } }
        static string? SafeStack(Exception ex) { try { return ex.StackTrace; } catch { return null; } }
        static string InnerChain(Exception ex)
        {
            var parts = new List<string>();
            try { for (var cur = ex.InnerException; cur != null && parts.Count < 8; cur = cur.InnerException) parts.Add((cur.GetType().FullName ?? cur.GetType().Name) + ": " + cur.Message); }
            catch (Exception e) { parts.Add("inner-capture-error: " + e.GetType().Name + ": " + e.Message); }
            return string.Join(" ---> ", parts);
        }
        static string Err(string n, Exception e) => Cut(n + ": " + e.GetType().FullName + ": " + e.Message) ?? n;
        static string Add(string? a, string b) => Cut(a == null ? b : a + " | " + b) ?? b;
        internal static object Snapshot()
        {
            try
            {
                if (!Enabled()) return new { enabled = false, totalExceptions = 0L, firstExceptionUtc = (string?)null, recentExceptionUtc = (string?)null, sampleIntervalSeconds = 1, sampleCapacity = Cap, samples = Array.Empty<Spt5LootPatrolFaultSample>() };
                lock (Gate) return new { enabled = true, totalExceptions = total, firstExceptionUtc = first?.ToString("o", CultureInfo.InvariantCulture), recentExceptionUtc = recent?.ToString("o", CultureInfo.InvariantCulture), sampleIntervalSeconds = 1, sampleCapacity = Cap, samples = Q.ToArray() };
            }
            catch (Exception e) { return new { enabled = false, totalExceptions = (long?)null, samples = Array.Empty<Spt5LootPatrolFaultSample>(), error = Cut(e.GetType().FullName + ": " + e.Message) }; }
        }
    }
}
