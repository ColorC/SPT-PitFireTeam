using System;
namespace pitTeam.Compat;
internal static class RadioAudioDurationPolicy
{
    internal static bool MatchesPcmDuration(int samples, int frequency, float actual, out float expected, out float tolerance)
    {
        expected = samples > 0 && frequency > 0 ? samples / (float)frequency : float.NaN;
        tolerance = samples > 0 && frequency > 0 ? 0.001f + (1f / frequency) + Math.Abs(expected) * 1.1920929e-7f * 2f : float.NaN;
        return samples > 0 && frequency > 0 && !float.IsNaN(actual) && !float.IsInfinity(actual) && actual > 0
            && !float.IsNaN(expected) && !float.IsInfinity(expected) && Math.Abs(actual - expected) <= tolerance;
    }
}

internal static class RadioAudioRecoveryPolicy
{
    internal static bool ShouldRebuild(bool currentOwner, bool resetPending, bool playbackInvalid, bool pcmCacheComplete, int currentFrame, int notBeforeFrame, int attempts, int maxAttempts)
    {
        return currentOwner && resetPending && playbackInvalid && pcmCacheComplete
            && currentFrame >= notBeforeFrame && attempts >= 0 && maxAttempts > 0 && attempts < maxAttempts;
    }
}
