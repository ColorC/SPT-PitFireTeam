using System;
using System.Collections.Generic;

namespace pitTeam.Compat
{
    internal static class Spt5BodyLootDiagnostics
    {
        internal readonly struct RouteFields
        {
            internal readonly string? Phrase, RequesterProfileId, BossProfileId, StoredCorpsePointer, FollowerProfileId;
            internal readonly bool? RequesterMatchesBoss;
            internal RouteFields(string? phrase,string? requesterProfileId,string? bossProfileId,bool? requesterMatchesBoss,string? storedCorpsePointer,string? followerProfileId=null)
            { Phrase=phrase; RequesterProfileId=requesterProfileId; BossProfileId=bossProfileId; RequesterMatchesBoss=requesterMatchesBoss; StoredCorpsePointer=storedCorpsePointer; FollowerProfileId=followerProfileId; }
        }
        internal readonly struct CandidateFields
        {
            internal readonly string? Reason;
            internal readonly float? NavDistance;
            internal readonly int? FreeArea;
            internal CandidateFields(string? reason, float? navDistance, int? freeArea)
            { Reason = reason; NavDistance = navDistance; FreeArea = freeArea; }
        }

        private const int MaximumEvents = 64;
        private static readonly object Gate = new object();
        private static readonly Queue<object> Events = new Queue<object>(MaximumEvents);
        private static long _eventSequence;

        private static bool IsEnabled()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT"));
        }

        public static bool CaptureEnabled => IsEnabled();

        public static void Record(string phase, string result, string? followerProfileId, string? targetPointer)
        {
            Enqueue(phase, result, followerProfileId, targetPointer, null);
        }

        public static void RecordCandidate(string result, string? reason, string? followerProfileId, float? navDistance, int? freeArea)
        {
            if (!IsEnabled()) return;
            Enqueue("candidate", result, followerProfileId, null, null, new CandidateFields(reason, navDistance, freeArea));
        }

        public static void RecordRoute(string phase, string result, Func<RouteFields> captureScalars)
        {
            try
            {
                if (!IsEnabled() || captureScalars == null) return;
                RouteFields fields = captureScalars();
                Enqueue(phase, result, fields.FollowerProfileId, fields.StoredCorpsePointer, fields);
            }
            catch { }
        }

        private static void Enqueue(string phase, string result, string? followerProfileId, string? targetPointer, RouteFields? route, CandidateFields? candidate = null)
        {
            try
            {
                if (!IsEnabled()) return;
                lock (Gate)
                {
                    Events.Enqueue(new
                    {
                        eventSequence = ++_eventSequence,
                        timestampUtc = DateTimeOffset.UtcNow,
                        phase,
                        result,
                        followerProfileId,
                        targetPointer,
                        phrase = route?.Phrase,
                        requesterProfileId = route?.RequesterProfileId,
                        bossProfileId = route?.BossProfileId,
                        requesterMatchesBoss = route?.RequesterMatchesBoss,
                        storedCorpsePointer = route?.StoredCorpsePointer,
                        reason = candidate?.Reason,
                        navDistance = candidate?.NavDistance,
                        freeArea = candidate?.FreeArea
                    });
                    while (Events.Count > MaximumEvents) Events.Dequeue();
                }
            }
            catch { }
        }
        public static object Snapshot()
        {
            try
            {
                bool enabled = IsEnabled();
                object[] events;
                lock (Gate) events = Events.ToArray();
                return new { enabled, producerAssembly = typeof(Spt5BodyLootDiagnostics).Assembly.GetName().Name, maximumEvents = MaximumEvents, events };
            }
            catch (Exception error)
            {
                return new { enabled = false, events = Array.Empty<object>(), error = error.ToString() };
            }
        }
    }
}
