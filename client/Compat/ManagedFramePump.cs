using System;
using System.Collections.Generic;
using System.Threading;

namespace pitTeam.Compat
{
    // Pure managed scheduling core: native access stays on the Unity owner thread.
    internal static class ManagedFramePump
    {
        private static readonly object Gate = new();
        private static readonly List<Func<bool>> Pending = new();
        private static int _ownerThread;
        private static OwnerSynchronizationContext? _managedContext;

        public static void Initialize()
        {
            int current = Environment.CurrentManagedThreadId;
            if (_ownerThread != 0 && _ownerThread != current)
                throw new InvalidOperationException("Frame pump already belongs to another thread.");
            _ownerThread = current;
            EnsureOwnerSynchronizationContext();
        }

        public static void RequireOwnerThread()
        {
            if (_ownerThread == 0 || _ownerThread != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("IL2CPP task conversion requires the initialized Unity thread.");
            EnsureOwnerSynchronizationContext();
        }

        private static void EnsureOwnerSynchronizationContext()
        {
            // Respect Unity or another host context already installed on the owner thread.
            if (SynchronizationContext.Current != null) return;
            if (_managedContext == null || !_managedContext.BelongsTo(_ownerThread))
                _managedContext = new OwnerSynchronizationContext(_ownerThread);
            SynchronizationContext.SetSynchronizationContext(_managedContext);
        }

        public static void Enqueue(Func<bool> job)
        {
            ArgumentNullException.ThrowIfNull(job);
            lock (Gate) Pending.Add(job);
        }

        public static void Tick()
        {
            RequireOwnerThread();
            Func<bool>[] batch;
            lock (Gate) { batch = Pending.ToArray(); Pending.Clear(); }
            foreach (var job in batch)
            {
                // Every bridge job translates its own faults. Unknown job failures stay visible.
                if (!job()) lock (Gate) Pending.Add(job);
            }
        }

        private sealed class OwnerSynchronizationContext : SynchronizationContext
        {
            private readonly int _ownerThread;

            public OwnerSynchronizationContext(int ownerThread) => _ownerThread = ownerThread;

            public bool BelongsTo(int ownerThread) => _ownerThread == ownerThread;

            public override void Post(SendOrPostCallback callback, object? state)
            {
                ArgumentNullException.ThrowIfNull(callback);
                EnsureActive();
                Enqueue(() =>
                {
                    EnsureActive();
                    callback(state);
                    return true;
                });
            }

            public override void Send(SendOrPostCallback callback, object? state)
            {
                ArgumentNullException.ThrowIfNull(callback);
                EnsureActive();
                if (Environment.CurrentManagedThreadId != _ownerThread)
                    throw new InvalidOperationException("Owner synchronization context Send cannot run off the Unity thread.");
                callback(state);
            }

            public override SynchronizationContext CreateCopy()
            {
                EnsureActive();
                return this;
            }

            private void EnsureActive()
            {
                if (_ownerThread == 0 || _ownerThread != ManagedFramePump._ownerThread ||
                    !ReferenceEquals(this, ManagedFramePump._managedContext))
                    throw new InvalidOperationException("Owner synchronization context is no longer active.");
            }
        }

#if MANAGED_FRAME_PUMP_TESTS
        internal static void ResetForTests()
        {
            lock (Gate) Pending.Clear();
            if (ReferenceEquals(SynchronizationContext.Current, _managedContext))
                SynchronizationContext.SetSynchronizationContext(null);
            _ownerThread = 0;
            _managedContext = null;
        }
#endif
    }
}
