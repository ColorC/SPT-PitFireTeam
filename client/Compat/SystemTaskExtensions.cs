using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace SPTushonka.Common.Utils
{
    internal static class SystemTaskExtensions
    {
        public static Task HandleExceptions(this Task task, [CallerMemberName] string operation = "")
        {
            if (task == null)
            {
                return null;
            }

            _ = task.ContinueWith(
                completed => LogFault(completed, operation),
                TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }

        public static Task<T> HandleExceptions<T>(this Task<T> task, [CallerMemberName] string operation = "")
        {
            if (task == null)
            {
                return null;
            }

            _ = task.ContinueWith(
                completed => LogFault(completed, operation),
                TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }

        private static void LogFault(Task task, string operation)
        {
            var exception = task.Exception;
            if (exception != null && pitTeam.pitFireTeam.Instance != null)
                pitTeam.pitFireTeam.Log.LogError($"[Async] {operation} failed: {exception}");
        }
    }
}
