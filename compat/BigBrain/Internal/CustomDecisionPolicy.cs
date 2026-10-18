namespace DrakiaXYZ.BigBrain.Internal
{
    internal static class CustomDecisionPolicy
    {
        internal static bool ShouldEndForDifferentCustomAction(int incomingActionId, int startLogicId, bool hasCurrentAction, bool hasRegisteredCurrentAction, int currentActionLogicId)
        {
            return hasCurrentAction && incomingActionId >= startLogicId && (!hasRegisteredCurrentAction || incomingActionId != currentActionLogicId);
        }
    }
}
