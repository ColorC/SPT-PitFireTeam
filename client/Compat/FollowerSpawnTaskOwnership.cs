using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace pitTeam.Compat
{
    internal static class FollowerSpawnTaskOwnership
    {
        internal static Task StartAndJoin<T>(IEnumerable<T> items, Func<T, Task> start)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (start == null) throw new ArgumentNullException(nameof(start));
            Task[] tasks = items.Select(start).ToArray();
            return Task.WhenAll(tasks);
        }
    }
}
