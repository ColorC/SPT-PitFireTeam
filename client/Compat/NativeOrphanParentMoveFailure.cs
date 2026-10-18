using System;

namespace pitTeam.Compat
{
    // Matches only the native IL2CPP exception shape observed at ItemManipulator.Move.
    internal static class NativeOrphanParentMoveFailure
    {
        private const string Il2CppExceptionType = "Il2CppInterop.Runtime.Il2CppException";
        private const string MissingParentMessage = "Trying to get parent of an item that doesn't have a parent";

        internal static bool Matches(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (Matches(current.GetType().FullName, current.Message))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool Matches(string exceptionTypeFullName, string message)
        {
            return string.Equals(exceptionTypeFullName, Il2CppExceptionType, StringComparison.Ordinal) &&
                message != null && message.IndexOf(MissingParentMessage, StringComparison.Ordinal) >= 0;
        }
    }
}
