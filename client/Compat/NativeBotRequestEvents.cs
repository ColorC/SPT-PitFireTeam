using System;

namespace pitTeam.Compat
{
    internal static class NativeBotRequestEvents
    {
        internal static void DetachVanillaGroupListener(BotRequestController controller)
        {
            var group = controller?._groupRequestController;
            var combined = group?.OnAddRequest;
            if (combined == null) return;
            foreach (var callback in combined.GetInvocationList())
            {
                var target = callback.Target?.TryCast<BotRequestController>();
                if (target?.Pointer != controller.Pointer || callback.Method?.Name != nameof(BotRequestController.OnAddRequest)) continue;
                var token = callback.TryCast<Il2CppSystem.Action<BotRequest>>();
                if (token != null) group.remove_OnAddRequest(token);
            }
        }
    }
}
