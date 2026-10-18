using System;
using System.Collections.Generic;
namespace pitTeam.Compat
{
    // A lease is the sole authority that may release or complete one pair.
    // This keeps a rejected reentrant callback from releasing another invocation's ownership.
    internal sealed class NormalBossLifecyclePairGate
    {
        private readonly object _sync = new object();
        private readonly HashSet<string> _completed = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _inFlight = new Dictionary<string, object>(StringComparer.Ordinal);
        public Lease TryAcquire(string key, out bool alreadyCompleted)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Pair key is required.", "key");
            lock (_sync)
            {
                alreadyCompleted = _completed.Contains(key);
                if (alreadyCompleted || _inFlight.ContainsKey(key)) return null;
                object owner = new object();
                _inFlight.Add(key, owner);
                return new Lease(this, key, owner);
            }
        }
        public int CompletedCount { get { lock (_sync) return _completed.Count; } }
        public int InFlightCount { get { lock (_sync) return _inFlight.Count; } }
        public void Clear()
        {
            lock (_sync)
            {
                _completed.Clear();
                _inFlight.Clear();
            }
        }
        private void Release(string key, object owner, bool complete)
        {
            lock (_sync)
            {
                object current;
                if (!_inFlight.TryGetValue(key, out current) || !ReferenceEquals(current, owner)) return;
                _inFlight.Remove(key);
                if (complete) _completed.Add(key);
            }
        }
        internal sealed class Lease : IDisposable
        {
            private NormalBossLifecyclePairGate _gate;
            private readonly string _key;
            private readonly object _owner;
            internal Lease(NormalBossLifecyclePairGate gate, string key, object owner)
            {
                _gate = gate;
                _key = key;
                _owner = owner;
            }
            public void Complete()
            {
                NormalBossLifecyclePairGate gate = _gate;
                if (gate == null) return;
                _gate = null;
                gate.Release(_key, _owner, true);
            }
            public void Dispose()
            {
                NormalBossLifecyclePairGate gate = _gate;
                if (gate == null) return;
                _gate = null;
                gate.Release(_key, _owner, false);
            }
        }
    }
}