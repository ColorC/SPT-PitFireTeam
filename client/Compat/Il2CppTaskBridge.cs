using System;
using System.Threading.Tasks;

namespace pitTeam.Compat
{
    internal static class Il2CppTaskBridge
    {
        public static Task FromNative(Il2CppSystem.Threading.Tasks.Task source)
        {
            ArgumentNullException.ThrowIfNull(source);
            ManagedFramePump.RequireOwnerThread();
            var result = new TaskCompletionSource<bool>();
            ManagedFramePump.Enqueue(() =>
            {
                try
                {
                    if (!source.IsCompleted) return false;
                    if (source.IsCanceled) result.TrySetCanceled();
                    else if (source.IsFaulted) result.TrySetException(NativeFault(source));
                    else result.TrySetResult(true);
                }
                catch (Exception error) { result.TrySetException(error); }
                return true;
            });
            return result.Task;
        }

        public static Task<T> FromNative<T>(Il2CppSystem.Threading.Tasks.Task<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            ManagedFramePump.RequireOwnerThread();
            var result = new TaskCompletionSource<T>();
            ManagedFramePump.Enqueue(() =>
            {
                try
                {
                    if (!source.IsCompleted) return false;
                    if (source.IsCanceled) result.TrySetCanceled();
                    else if (source.IsFaulted) result.TrySetException(NativeFault(source));
                    else result.TrySetResult(source.Result);
                }
                catch (Exception error) { result.TrySetException(error); }
                return true;
            });
            return result.Task;
        }

        public static Il2CppSystem.Threading.Tasks.Task ToNative(Task source)
        {
            ArgumentNullException.ThrowIfNull(source);
            ManagedFramePump.RequireOwnerThread();
            var result = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<int>();
            ManagedFramePump.Enqueue(() =>
            {
                if (!source.IsCompleted) return false;
                Complete(source, result, () => 0);
                return true;
            });
            return result.Task;
        }

        public static Il2CppSystem.Threading.Tasks.Task<T> ToNative<T>(Task<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            ManagedFramePump.RequireOwnerThread();
            var result = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<T>();
            ManagedFramePump.Enqueue(() =>
            {
                if (!source.IsCompleted) return false;
                Complete(source, result, () => source.GetAwaiter().GetResult());
                return true;
            });
            return result.Task;
        }

        private static Exception NativeFault(Il2CppSystem.Threading.Tasks.Task source) =>
            new InvalidOperationException("Native task failed: " + source.Exception?.ToString());

        private static void Complete<T>(Task source,
            Il2CppSystem.Threading.Tasks.TaskCompletionSource<T> result, Func<T> value)
        {
            // Called by the Unity frame pump, including fault construction in the native runtime.
            if (source.IsCanceled) { result.TrySetCanceled(); return; }
            if (source.IsFaulted)
            {
                result.TrySetException(new Il2CppSystem.Exception(source.Exception!.ToString()));
                return;
            }
            try { result.TrySetResult(value()); }
            catch (Exception error)
            {
                result.TrySetException(new Il2CppSystem.Exception(error.ToString()));
            }
        }
    }
}
