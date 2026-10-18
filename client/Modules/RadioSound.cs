using Comfort.Common;
using EFT;
using EFT.UI;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace pitTeam.Modules
{
    internal class RadioSound : MonoBehaviour
    {
        public RadioSound(IntPtr pointer) : base(pointer) { }


        private AudioClip radioClip;
        private AudioClip locationClip;
        private AudioClip radioBeepClip;
        private static RadioSound currentOwner;
        private readonly List<DownloadHandler> ownedDownloadHandlers = new List<DownloadHandler>();
        private readonly List<AudioClip> ownedAudioClips = new List<AudioClip>();
        private readonly List<HideFlags> ownedAudioClipOriginalFlags = new List<HideFlags>();
        private readonly HashSet<IntPtr> clipDimensionsObservedValid = new HashSet<IntPtr>();
        private readonly HashSet<IntPtr> clipDimensionLossRecorded = new HashSet<IntPtr>();
        private readonly Queue<object> clipDimensionLossDiagnostics = new Queue<object>();
        private readonly Queue<object> playbackDispatchDiagnostics = new Queue<object>();
        private string playbackDispatchDiagnosticError;
        private AudioSettings.AudioConfigurationChangeHandler audioConfigurationHandler;
        private Action<bool> audioConfigurationCallback;
        private int audioConfigurationChangeCount;
        private object lastAudioConfigurationChange;
        private int playbackLoadAudioDataCalls;
        private int? playbackLoadAudioDataFrame;
        private int ownedClipReleaseCount;
        private string lastOwnedClipReleasedPointer;
        private string lastOwnedClipReleaseReason;
        private int? lastOwnedClipReleaseFrame;
        private sealed class CachedPcmAudio
        {
            internal readonly float[] Samples;
            internal readonly int SampleFrames;
            internal readonly int Channels;
            internal readonly int Frequency;
            internal readonly IntPtr SourcePointer;
            internal readonly string FileName;
            internal CachedPcmAudio(float[] samples, int sampleFrames, int channels, int frequency, IntPtr sourcePointer, string fileName)
            { Samples = samples; SampleFrames = sampleFrames; Channels = channels; Frequency = frequency; SourcePointer = sourcePointer; FileName = fileName; }
        }
        private readonly Dictionary<string, CachedPcmAudio> cachedPcmBySlot = new Dictionary<string, CachedPcmAudio>(StringComparer.Ordinal);
        private long audioConfigurationEpoch;
        private long audioRecoveryHandledEpoch;
        private long audioRecoveryGeneration;
        private int audioRecoveryAttemptCount;
        private int? audioRecoveryNotBeforeFrame;
        private int audioRecoverySuccessCount;
        private int? lastAudioRecoveryFrame;
        private string audioRecoveryStatus = "idle";
        private string audioRecoveryError;
        private const int AudioRecoveryMaxAttempts = 3;
        private const int AudioRecoveryRetryDelayFrames = 30;
        private bool isDestroyed;

        private Task enableTask;
        private bool enableTaskObserved;
        private static readonly Queue<object> clipLoadDiagnostics = new Queue<object>();
        private static string clipLoadDiagnosticError;
        private int? destroyedFrame;
        private int clipCopySequence;
        private readonly Queue<object> executionContextDiagnostics = new Queue<object>();
        private int? lastUnityUpdateThreadId;
        private string lastUnityUpdateContextType;
        private int? lastUnityUpdateFrame;

        [HideFromIl2Cpp]
        internal object GetLoadProbeSnapshot() => new { events = clipLoadDiagnostics.ToArray(), executionContexts = executionContextDiagnostics.ToArray(), error = clipLoadDiagnosticError, destroyedFrame, ownerPointer = Pointer.ToString("X"), ownerIsCurrent = ReferenceEquals(currentOwner, this), lastUnityUpdateThreadId, lastUnityUpdateContextType, lastUnityUpdateFrame, clipDimensionLossDiagnostics = clipDimensionLossDiagnostics.ToArray(), audioConfigurationChangeCount, lastAudioConfigurationChange, playbackLoadAudioDataCalls, playbackLoadAudioDataFrame, ownedClipReleaseCount, lastOwnedClipReleasedPointer, lastOwnedClipReleaseReason, lastOwnedClipReleaseFrame, ownedAudioClipCount = ownedAudioClips.Count, cachedPcmCount = cachedPcmBySlot.Count, audioConfigurationEpoch, audioRecoveryHandledEpoch, audioRecoveryGeneration, audioRecoveryAttemptCount, audioRecoverySuccessCount, lastAudioRecoveryFrame, audioRecoveryNotBeforeFrame, audioRecoveryStatus, audioRecoveryError, playbackDispatches = playbackDispatchDiagnostics.ToArray(), playbackDispatchDiagnosticError };
        [HideFromIl2Cpp]
        private void RecordPlaybackDispatch(string consumer, string route, AudioClip clip, bool callReturned, BetterSource betterSource, string error)
        {
            try
            {
                bool clipReferencePresent = !ReferenceEquals(clip, null);
                bool clipAlive = clip != null;
                string clipPointer = null;
                string clipLoadState = null;
                bool? clipReady = null;
                float? clipLength = null;
                int? clipSamples = null, clipChannels = null, clipFrequency = null;
                string clipReadError = null;
                try
                {
                    if (clipReferencePresent) clipPointer = clip.Pointer.ToString("X");
                    if (clipAlive)
                    {
                        clipLoadState = clip.loadState.ToString(); clipReady = clip.isReadyToPlay; clipLength = clip.length;
                        clipSamples = clip.samples; clipChannels = clip.channels; clipFrequency = clip.frequency;
                    }
                }
                catch (Exception readError) { clipReadError = readError.ToString(); }
                bool betterSourceReferencePresent = !ReferenceEquals(betterSource, null);
                bool betterSourceAlive = betterSource != null;
                string betterSourcePointer = null, betterSourcePlayBackState = null, betterSourceReadError = null;
                bool? betterSourceIsActive = null, betterSourceAudioSourceIsPlaying = null;
                string audioSourcePointer = null;
                try
                {
                    if (betterSourceReferencePresent) betterSourcePointer = betterSource.Pointer.ToString("X");
                    if (betterSourceAlive)
                    {
                        betterSourcePlayBackState = betterSource.PlayBackState.ToString();
                        betterSourceIsActive = betterSource.IsActive;
                        AudioSource audioSource = betterSource.Source1;
                        if (audioSource != null) { audioSourcePointer = audioSource.Pointer.ToString("X"); betterSourceAudioSourceIsPlaying = audioSource.isPlaying; }
                    }
                }
                catch (Exception readError) { betterSourceReadError = readError.ToString(); }
                string playbackObservation = route.IndexOf("GUISounds.PlaySound", StringComparison.Ordinal) >= 0
                    ? (callReturned ? "not-observed-void-return" : "not-observed-call-failed")
                    : !callReturned ? "call-failed"
                    : !betterSourceAlive ? "better-source-null"
                    : betterSourcePlayBackState == null ? "better-source-state-unreadable"
                    : betterSourcePlayBackState;
                playbackDispatchDiagnostics.Enqueue(new
                {
                    consumer,
                    frame = Time.frameCount,
                    threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                    ownerPointer = Pointer.ToString("X"),
                    route,
                    callReturned,
                    clip = new { referencePresent = clipReferencePresent, unityObjectAlive = clipAlive, pointer = clipPointer, loadState = clipLoadState, isReadyToPlay = clipReady, length = clipLength, samples = clipSamples, channels = clipChannels, frequency = clipFrequency, readError = clipReadError },
                    betterSource = new { referencePresent = betterSourceReferencePresent, unityObjectAlive = betterSourceAlive, pointer = betterSourcePointer, playBackState = betterSourcePlayBackState, isActive = betterSourceIsActive, audioSourcePointer, audioSourceIsPlaying = betterSourceAudioSourceIsPlaying, readError = betterSourceReadError },
                    playbackObservation,
                    error
                });
                while (playbackDispatchDiagnostics.Count > 12) playbackDispatchDiagnostics.Dequeue();
            }
            catch (Exception diagnosticError) { playbackDispatchDiagnosticError = diagnosticError.ToString(); }
        }

        [HideFromIl2Cpp]
        private void PlayGuiSoundAndRecord(string consumer, string route, AudioClip clip, float level)
        {
            try
            {
                Singleton<GUISounds>.Instance.PlaySound(clip, false, true, level);
                RecordPlaybackDispatch(consumer, route, clip, true, null, null);
            }
            catch (Exception error)
            {
                RecordPlaybackDispatch(consumer, route, clip, false, null, error.ToString());
                throw;
            }
        }
        [HideFromIl2Cpp]
        private void RecordExecutionContext(string stage)
        {
            try
            {
                executionContextDiagnostics.Enqueue(new { stage, threadId = System.Threading.Thread.CurrentThread.ManagedThreadId, syncContextType = System.Threading.SynchronizationContext.Current == null ? null : System.Threading.SynchronizationContext.Current.GetType().FullName, ownerPointer = Pointer.ToString("X"), ownerIsCurrent = ReferenceEquals(currentOwner, this), destroyed = isDestroyed });
                while (executionContextDiagnostics.Count > 32) executionContextDiagnostics.Dequeue();
            }
            catch (Exception error) { clipLoadDiagnosticError = error.ToString(); }
        }

        [HideFromIl2Cpp]
        private void RegisterAudioConfigurationObserver()
        {
            try
            {
                if (audioConfigurationHandler != null) return;
                audioConfigurationCallback = RecordAudioConfigurationChange;
                audioConfigurationHandler = audioConfigurationCallback;
                AudioSettings.OnAudioConfigurationChanged += audioConfigurationHandler;
            }
            catch (Exception error) { clipLoadDiagnosticError = "audio-config-register: " + error; }
        }

        [HideFromIl2Cpp]
        private void UnregisterAudioConfigurationObserver()
        {
            try
            {
                if (audioConfigurationHandler != null) AudioSettings.OnAudioConfigurationChanged -= audioConfigurationHandler;
            }
            catch (Exception error) { clipLoadDiagnosticError = "audio-config-unregister: " + error; }
            finally { audioConfigurationHandler = null; audioConfigurationCallback = null; }
        }

        [HideFromIl2Cpp]
        private void RecordAudioConfigurationChange(bool deviceWasChanged)
        {
            try
            {
                audioConfigurationChangeCount++;
                audioConfigurationEpoch++;
                audioRecoveryGeneration++;
                audioRecoveryAttemptCount = 0;
                audioRecoveryNotBeforeFrame = Time.frameCount + 1;
                audioRecoveryStatus = "scheduled-after-configuration-change";
                audioRecoveryError = null;
                lastAudioConfigurationChange = new { frame = Time.frameCount, threadId = System.Threading.Thread.CurrentThread.ManagedThreadId, deviceWasChanged, ownerPointer = Pointer.ToString("X") };
            }
            catch (Exception error) { clipLoadDiagnosticError = "audio-config-callback: " + error; }
        }
        [HideFromIl2Cpp]
        private void ObserveOwnedClipDimensions()
        {
            for (int i = 0; i < ownedAudioClips.Count; i++)
            {
                try
                {
                    AudioClip clip = ownedAudioClips[i];
                    if (clip == null) continue;
                    IntPtr pointer = clip.Pointer;
                    if (pointer == IntPtr.Zero) continue;
                    int samples = clip.samples, channels = clip.channels, frequency = clip.frequency;
                    float length = clip.length;
                    bool valid = samples > 0 && channels > 0 && frequency > 0 && !float.IsNaN(length) && !float.IsInfinity(length) && length > 0;
                    if (valid) clipDimensionsObservedValid.Add(pointer);
                    else if (clipDimensionsObservedValid.Contains(pointer) && clipDimensionLossRecorded.Add(pointer))
                    {
                        clipDimensionLossDiagnostics.Enqueue(new { frame = Time.frameCount, pointer = pointer.ToString("X"), samples, channels, frequency, length, loadState = clip.loadState.ToString(), loadType = clip.loadType.ToString() });
                        while (clipDimensionLossDiagnostics.Count > 6) clipDimensionLossDiagnostics.Dequeue();
                    }
                }
                catch (Exception error) { clipLoadDiagnosticError = "owned-clip-dimension-observer: " + error; }
            }
        }
        [HideFromIl2Cpp]
        private static bool IsPlaybackUsable(AudioClip clip)
        {
            return clip != null && clip.loadState.ToString() == "Loaded" && clip.isReadyToPlay && clip.samples > 0 && clip.channels > 0 && clip.frequency > 0 && pitTeam.Compat.RadioAudioDurationPolicy.MatchesPcmDuration(clip.samples, clip.frequency, clip.length, out _, out _) && !string.Equals(clip.loadType.ToString(), "Streaming", StringComparison.Ordinal);
        }
        [HideFromIl2Cpp]
        private void TryRecoverAfterAudioConfigurationChange()
        {
            if (isDestroyed || audioConfigurationEpoch == audioRecoveryHandledEpoch) return;
            if (!ReferenceEquals(currentOwner, this)) { audioRecoveryStatus = "pending-not-current-owner"; return; }
            int frame = Time.frameCount;
            if (audioRecoveryNotBeforeFrame.HasValue && frame < audioRecoveryNotBeforeFrame.Value) return;
            if (enableTask != null && !enableTask.IsCompleted) return;
            bool invalid = !IsPlaybackUsable(radioClip) || !IsPlaybackUsable(locationClip) || !IsPlaybackUsable(radioBeepClip);
            if (!invalid) { audioRecoveryHandledEpoch = audioConfigurationEpoch; audioRecoveryStatus = "reset-no-playback-loss"; audioRecoveryError = null; return; }
            bool cachesReady = cachedPcmBySlot.ContainsKey("radiochat") && cachedPcmBySlot.ContainsKey("locationping") && cachedPcmBySlot.ContainsKey("radiobeep");
            if (!cachesReady) { audioRecoveryStatus = "pending-pcm-cache-incomplete"; audioRecoveryError = "Required decoded PCM cache is not yet available for all three clips."; audioRecoveryNotBeforeFrame = frame + AudioRecoveryRetryDelayFrames; return; }
            if (audioRecoveryAttemptCount >= AudioRecoveryMaxAttempts) { audioRecoveryStatus = "pending-retry-limit"; return; }
            if (!pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(ReferenceEquals(currentOwner, this), audioConfigurationEpoch != audioRecoveryHandledEpoch, invalid, cachesReady, frame, audioRecoveryNotBeforeFrame.GetValueOrDefault(frame), audioRecoveryAttemptCount, AudioRecoveryMaxAttempts)) return;
            long generation = audioRecoveryGeneration; long epoch = audioConfigurationEpoch;
            AudioClip[] replacements = new AudioClip[3]; AudioClip[] previous = { radioClip, locationClip, radioBeepClip }; bool published = false;
            audioRecoveryAttemptCount++; audioRecoveryStatus = "rebuilding-from-managed-pcm"; audioRecoveryError = null;
            try
            {
                string[] keys = { "radiochat", "locationping", "radiobeep" };
                for (int i = 0; i < keys.Length; i++)
                {
                    CachedPcmAudio cached = cachedPcmBySlot[keys[i]];
                    Il2CppStructArray<float> pcm = CreateInteropPcm(cached.Samples);
                    AudioClip clip = CreateIndependentPcmPlaybackClip(cached.SourcePointer, pcm, cached.SampleFrames, cached.Channels, cached.Frequency, out bool? setData);
                    replacements[i] = clip;
                    if (setData != true || clip.loadState.ToString() != "Loaded" || !clip.isReadyToPlay || clip.GetData(new Il2CppStructArray<float>(cached.Samples.Length), 0) != true)
                        throw new InvalidOperationException("Recovered PCM clip did not satisfy SetData/Loaded/ready/GetData readback guards: " + cached.FileName);
                    if (!PinOwnedAudioClip(clip)) throw new ObjectDisposedException("RadioSound");
                }
                if (isDestroyed || !ReferenceEquals(currentOwner, this) || generation != audioRecoveryGeneration || epoch != audioConfigurationEpoch)
                    throw new OperationCanceledException("Audio configuration generation changed before PCM recovery publish.");
                radioClip = replacements[0]; locationClip = replacements[1]; radioBeepClip = replacements[2]; published = true;
                for (int i = 0; i < previous.Length; i++) if (previous[i] != null && !ReferenceEquals(previous[i], replacements[i])) ReleaseOwnedAudioClip(previous[i], true, "audio-config-recovered");
                audioRecoveryHandledEpoch = epoch; audioRecoverySuccessCount++; lastAudioRecoveryFrame = frame; audioRecoveryStatus = "recovered-from-managed-pcm"; audioRecoveryError = null;
            }
            catch (Exception error)
            {
                audioRecoveryError = error.ToString(); audioRecoveryStatus = audioRecoveryAttemptCount >= AudioRecoveryMaxAttempts ? "pending-retry-limit" : "pending-retry"; audioRecoveryNotBeforeFrame = frame + AudioRecoveryRetryDelayFrames;
                if (!published) for (int i = 0; i < replacements.Length; i++) if (replacements[i] != null) ReleaseOwnedAudioClip(replacements[i], true, "audio-config-recovery-failed");
            }
        }
        public void OnDestroy()
        {
            try { destroyedFrame = Time.frameCount; }
            catch (Exception error) { clipLoadDiagnosticError = error.ToString(); }
            finally
            {
                UnregisterAudioConfigurationObserver();
                isDestroyed = true;
                if (ReferenceEquals(currentOwner, this)) currentOwner = null;
                try { ReleaseAllOwnedAudioClips(); cachedPcmBySlot.Clear(); audioRecoveryStatus = "owner-destroyed"; }
                finally { DisposeOwnedDownloadHandlers(); }
            }
        }

        [HideFromIl2Cpp]
        private void DisposeOwnedDownloadHandlers()
        {
            for (int i = ownedDownloadHandlers.Count - 1; i >= 0; i--)
            {
                DownloadHandler handler = ownedDownloadHandlers[i];
                if (handler == null) continue;
                try { handler.Dispose(); }
                catch (Exception error) { clipLoadDiagnosticError = error.ToString(); }
            }
            ownedDownloadHandlers.Clear();
        }

        [HideFromIl2Cpp]
        private bool PinOwnedAudioClip(AudioClip clip)
        {
            if (isDestroyed || clip == null) return false;
            bool clipFound = false;
            for (int i = 0; i < ownedAudioClips.Count; i++)
            {
                if (!ReferenceEquals(ownedAudioClips[i], clip)) continue;
                clipFound = true;
                break;
            }
            if (!clipFound)
            {
                HideFlags originalFlags = clip.hideFlags;
                clip.hideFlags = originalFlags | HideFlags.DontUnloadUnusedAsset;
                ownedAudioClips.Add(clip);
                ownedAudioClipOriginalFlags.Add(originalFlags);
            }

            return true;
        }

        [HideFromIl2Cpp]
        private bool RetainOwnedDownloadHandler(DownloadHandler handler)
        {
            if (isDestroyed || handler == null) return false;
            for (int i = 0; i < ownedDownloadHandlers.Count; i++)
                if (ReferenceEquals(ownedDownloadHandlers[i], handler)) return true;
            ownedDownloadHandlers.Add(handler);
            return true;
        }
        [HideFromIl2Cpp]
        private bool IsOwnedAudioClip(AudioClip clip)
        {
            for (int i = 0; i < ownedAudioClips.Count; i++)
                if (ReferenceEquals(ownedAudioClips[i], clip)) return true;
            return false;
        }

        [HideFromIl2Cpp]
        private void ReplaceOwnedAudioClip(ref AudioClip slot, AudioClip replacement)
        {
            if (replacement == null) return;
            AudioClip previous = slot;
            if (!ReferenceEquals(previous, replacement)) ReleaseOwnedAudioClip(previous, true, "replace-slot");
            slot = replacement;
        }

        [HideFromIl2Cpp]
        private void ReleaseOwnedAudioClip(AudioClip clip, bool destroy, string reason)
        {
            for (int i = ownedAudioClips.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(ownedAudioClips[i], clip)) continue;
                AudioClip owned = ownedAudioClips[i];
                try
                {
                    ownedClipReleaseCount++;
                    lastOwnedClipReleasedPointer = owned == null ? null : owned.Pointer.ToString("X");
                    lastOwnedClipReleaseReason = reason;
                    lastOwnedClipReleaseFrame = Time.frameCount;
                }
                catch (Exception error) { clipLoadDiagnosticError = "owned-clip-release-observer: " + error; }
                HideFlags originalFlags = ownedAudioClipOriginalFlags[i];
                ownedAudioClips.RemoveAt(i);
                ownedAudioClipOriginalFlags.RemoveAt(i);
                if (owned != null)
                {
                    try { owned.hideFlags = originalFlags; }
                    catch (Exception error) { clipLoadDiagnosticError = error.ToString(); }
                    if (destroy)
                    {
                        try { UnityEngine.Object.Destroy(owned); }
                        catch (Exception error) { clipLoadDiagnosticError = error.ToString(); }
                    }
                }
                return;
            }
            if (destroy && clip != null)
            {
                try { UnityEngine.Object.Destroy(clip); }
                catch (Exception error) { clipLoadDiagnosticError = error.ToString(); }
            }
        }

        [HideFromIl2Cpp]
        private void ReleaseAllOwnedAudioClips()
        {
            while (ownedAudioClips.Count > 0)
                ReleaseOwnedAudioClip(ownedAudioClips[ownedAudioClips.Count - 1], true, "owner-destroy");
            ownedAudioClipOriginalFlags.Clear();
            radioClip = null;
            locationClip = null;
            radioBeepClip = null;
        }
        [HideFromIl2Cpp]
        private void RecordClipLoad(string uri, bool? disposeFlagReadback, bool? streamAudio, bool? compressed,
            bool? handlerIsDone, int? handlerDataBytes, string handlerDataStatus, string handlerDataError, float? sourceLength, int? sourceSamples, int? sourceChannels,
            int? sourceFrequency, string sourceLoadType, bool? sourceGetDataSucceeded, float? playbackLength,
            int? playbackSamples, int? playbackChannels, int? playbackFrequency, string playbackLoadType, string playbackHideFlags,
            string playbackStrategy, string sourceClipPointer, string playbackClipPointer, bool? playbackSetDataSucceeded, bool? playbackGetDataSucceeded, bool playbackIsDownloadedSource, bool sourceClipPinned, bool handlerRetained, bool handlerDisposed, bool requestDisposed, bool playbackClipPinned, AudioClip playbackClip, string failure)
        {
            try
            {
                string playbackPointerAfterRequestDispose = null;
                bool? playbackAliveAfterRequestDispose = null;
                float? postDisposeLength = null;
                int? postDisposeSamples = null;
                int? postDisposeChannels = null;
                int? postDisposeFrequency = null;
                string postDisposeLoadState = null;
                string postDisposeHideFlags = null;
                string postDisposeReadError = null;
                try
                {
                    if (!ReferenceEquals(playbackClip, null))
                    {
                        playbackPointerAfterRequestDispose = playbackClip.Pointer.ToString("X");
                        playbackAliveAfterRequestDispose = playbackClip != null;
                        if (playbackAliveAfterRequestDispose == true)
                        {
                            postDisposeLength = playbackClip.length;
                            postDisposeSamples = playbackClip.samples;
                            postDisposeChannels = playbackClip.channels;
                            postDisposeFrequency = playbackClip.frequency;
                            postDisposeLoadState = playbackClip.loadState.ToString();
                            postDisposeHideFlags = playbackClip.hideFlags.ToString();
                        }
                    }
                }
                catch (Exception readbackError) { postDisposeReadError = readbackError.ToString(); }
                clipLoadDiagnostics.Enqueue(new
                {
                    fileName = Path.GetFileName(uri),
                    frame = Time.frameCount,
                    disposeDownloadHandlerOnDispose = disposeFlagReadback,
                    streamAudio,
                    compressed,
                    handlerIsDone,
                    handlerDataBytes,
                    handlerDataStatus,
                    handlerDataError,
                    source = new { pointer = sourceClipPointer, length = sourceLength, samples = sourceSamples, channels = sourceChannels, frequency = sourceFrequency, loadType = sourceLoadType, getDataSucceeded = sourceGetDataSucceeded },
                    playback = new { strategy = playbackStrategy, sourcePointer = sourceClipPointer, pointer = playbackClipPointer, setDataSucceeded = playbackSetDataSucceeded, getDataSucceeded = playbackGetDataSucceeded, isDownloadedSource = playbackIsDownloadedSource, length = playbackLength, samples = playbackSamples, channels = playbackChannels, frequency = playbackFrequency, loadType = playbackLoadType, hideFlags = playbackHideFlags, pinned = playbackClipPinned },
                    aliveBeforeRequestDispose = sourceSamples.HasValue && sourceSamples.Value > 0,
                    aliveAfterRequestDispose = playbackAliveAfterRequestDispose,
                    playbackPointerAfterRequestDispose,
                    afterHandlerAndRequestDispose = new { length = postDisposeLength, samples = postDisposeSamples, channels = postDisposeChannels, frequency = postDisposeFrequency, loadState = postDisposeLoadState, hideFlags = postDisposeHideFlags, readError = postDisposeReadError },
                    afterRequestDispose = new { length = postDisposeLength, samples = postDisposeSamples, channels = postDisposeChannels, frequency = postDisposeFrequency, loadState = postDisposeLoadState, hideFlags = postDisposeHideFlags, readError = postDisposeReadError },
                    sourceClipPinned,
                    handlerRetained,
                    clipHideFlags = playbackHideFlags,
                    clipPinned = playbackClipPinned,
                    handlerDisposed,
                    requestDisposed,
                    ownerAlive = !isDestroyed,
                    failure
                });
                while (clipLoadDiagnostics.Count > 3) clipLoadDiagnostics.Dequeue();
            }
            catch (Exception error) { clipLoadDiagnosticError = error.ToString(); }
        }

        [HideFromIl2Cpp]
        public Task EnableAsync()
        {
            if (isDestroyed) return Task.FromException(new ObjectDisposedException("RadioSound"));
            currentOwner = this;
            RegisterAudioConfigurationObserver();
            if (enableTask == null) enableTask = EnableCoreAsync();
            return enableTask;
        }

        [HideFromIl2Cpp]
        private async Task EnableCoreAsync()
        {
            long generation = audioRecoveryGeneration;
            List<AudioClip> sounds = await GetSound();
            if (isDestroyed || !ReferenceEquals(currentOwner, this) || generation != audioRecoveryGeneration)
            {
                foreach (AudioClip clip in sounds) ReleaseOwnedAudioClip(clip, true, "stale-audio-generation");
                if (!isDestroyed && ReferenceEquals(currentOwner, this)) { audioRecoveryStatus = "pending-stale-initial-load"; audioRecoveryNotBeforeFrame = Time.frameCount + 1; }
                return;
            }
            ReplaceOwnedAudioClip(ref radioClip, sounds[0]);
            ReplaceOwnedAudioClip(ref locationClip, sounds[1]);
            ReplaceOwnedAudioClip(ref radioBeepClip, sounds[2]);
        }

        public void Update()
        {
            lastUnityUpdateThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            lastUnityUpdateContextType = System.Threading.SynchronizationContext.Current == null ? null : System.Threading.SynchronizationContext.Current.GetType().FullName;
            lastUnityUpdateFrame = Time.frameCount;
            ObserveOwnedClipDimensions();
            if (!enableTaskObserved && enableTask != null && enableTask.IsCompleted)
            {
                enableTaskObserved = true;
                if (enableTask.IsFaulted) Logger.LogError(enableTask.Exception.GetBaseException());
                else if (enableTask.IsCanceled) Logger.LogError(new TaskCanceledException(enableTask).Message);
            }
            TryRecoverAfterAudioConfigurationChange();
        }

        [HideFromIl2Cpp]
        private async Task<List<AudioClip>> GetSound()
        {
            List<AudioClip> audioClips = new List<AudioClip>();
            audioClips.Add(await LoadAudioClip("file://" + ResolveSoundPath("radiochat.ogg")));
            audioClips.Add(await LoadAudioClip("file://" + ResolveSoundPath("locationping.ogg")));
            audioClips.Add(await LoadAudioClip("file://" + ResolveSoundPath("radiobeep.ogg")));

            return audioClips;
        }


        [HideFromIl2Cpp]
        private Il2CppStructArray<float> CreateInteropPcm(float[] samples)
        {
            if (samples == null || samples.Length == 0) throw new InvalidOperationException("Cached PCM is empty.");
            var result = new Il2CppStructArray<float>(samples.Length);
            for (int i = 0; i < samples.Length; i++) result[i] = samples[i];
            return result;
        }
        [HideFromIl2Cpp]
        private AudioClip CreateIndependentPcmPlaybackClip(IntPtr sourcePointer, Il2CppStructArray<float> pcm,
            int sourceSamples, int sourceChannels, int sourceFrequency,
            out bool? setDataSucceeded)
        {
            setDataSucceeded = null;
            if (sourceSamples <= 0 || sourceChannels <= 0 || sourceFrequency <= 0)
                throw new InvalidOperationException("Independent PCM dimensions must be positive.");
            AudioClip playbackClip = AudioClip.Create("pitFireTeam:radio-pcm", sourceSamples, sourceChannels, sourceFrequency, false);
            if (playbackClip == null) throw new InvalidOperationException("AudioClip.Create returned a null PCM playback clip.");
            bool accepted = false;
            try
            {
            if (playbackClip.Pointer == sourcePointer)
                throw new InvalidOperationException("AudioClip.Create returned the downloaded source pointer instead of an independent clip.");
            setDataSucceeded = playbackClip.SetData(pcm, 0);
            if (setDataSucceeded != true) throw new InvalidOperationException("Independent AudioClip.SetData returned false for copied PCM.");
            float playbackLength = playbackClip.length;
            bool validPlaybackLength = pitTeam.Compat.RadioAudioDurationPolicy.MatchesPcmDuration(sourceSamples, sourceFrequency, playbackLength, out float expectedPcmLength, out float durationTolerance);
            if (playbackClip.samples != sourceSamples || playbackClip.channels != sourceChannels
                || playbackClip.frequency != sourceFrequency || !validPlaybackLength
                || string.Equals(playbackClip.loadType.ToString(), "Streaming", StringComparison.Ordinal))
                throw new InvalidOperationException("Independent PCM guard failed: playbackPointer=" + playbackClip.Pointer.ToString("X")
                    + "; sourcePointer=" + sourcePointer.ToString("X") + "; pointerIndependent=" + (playbackClip.Pointer != sourcePointer)
                    + "; setData=" + setDataSucceeded + "; samples=" + playbackClip.samples + "/" + sourceSamples
                    + "; samplesMatch=" + (playbackClip.samples == sourceSamples) + "; channels=" + playbackClip.channels + "/" + sourceChannels
                    + "; channelsMatch=" + (playbackClip.channels == sourceChannels) + "; frequency=" + playbackClip.frequency + "/" + sourceFrequency
                    + "; frequencyMatches=" + (playbackClip.frequency == sourceFrequency)
                    + "; length=" + playbackLength.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + "; expectedPcmLength=" + expectedPcmLength.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + "; tolerance=" + durationTolerance.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + "; durationMatches=" + validPlaybackLength
                    + "; loadState=" + playbackClip.loadState.ToString() + "; loadType=" + playbackClip.loadType.ToString()
                    + "; nonStreaming=" + !string.Equals(playbackClip.loadType.ToString(), "Streaming", StringComparison.Ordinal));
            accepted = true;
            return playbackClip;
            }
            finally
            {
                if (!accepted && playbackClip != null)
                {
                    try { UnityEngine.Object.Destroy(playbackClip); }
                    catch { }
                }
            }
        }
        [HideFromIl2Cpp]
        private Task<AudioClip> LoadAudioClip(string uri)
        {
            string fileName = Path.GetFileName(uri);
            string slotKey = fileName.StartsWith("radiochat.ogg", StringComparison.OrdinalIgnoreCase) ? "radiochat"
                : fileName.StartsWith("locationping.ogg", StringComparison.OrdinalIgnoreCase) ? "locationping"
                : fileName.StartsWith("radiobeep.ogg", StringComparison.OrdinalIgnoreCase) ? "radiobeep"
                : throw new ArgumentException("Unsupported radio clip key.", nameof(uri));
            return LoadAudioClip(uri, slotKey);
        }
        [HideFromIl2Cpp]
        private async Task<AudioClip> LoadAudioClip(string uri, string slotKey)
        {
            RecordExecutionContext("load-enter:" + Path.GetFileName(uri));
            AudioClip sourceClip = null;
            AudioClip playbackClip = null;
            UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS);
            DownloadHandler handler = null;
            DownloadHandlerAudioClip audioHandler = null;
            bool requestOwnsHandler = true;
            bool playbackClipPinned = false;
            bool sourceClipPinned = false;
            bool handlerRetained = false;
            bool playbackIsDownloadedSource = false;
            string playbackStrategy = null;
            string playbackClipPointer = null;
            string sourceClipPointer = null;
            bool? playbackSetDataSucceeded = null;
            bool? playbackGetDataSucceeded = null;
            bool handlerDisposed = false;
            bool requestDisposed = false;
            bool? disposeFlagReadback = null;
            bool? streamAudioReadback = null;
            bool? compressedReadback = null;
            bool? handlerIsDone = null;
            int? handlerDataBytes = null;
            string handlerDataStatus = null;
            string handlerDataError = null;
            int? sourceSamples = null;
            int? sourceChannels = null;
            int? sourceFrequency = null;
            float? sourceLength = null;
            string sourceLoadType = null;
            bool? sourceGetDataSucceeded = null;
            int? playbackSamples = null;
            int? playbackChannels = null;
            int? playbackFrequency = null;
            float? playbackLength = null;
            string playbackLoadType = null;
            string playbackHideFlags = null;
            string failure = null;
            try
            {
                handler = req.downloadHandler;
                audioHandler = handler == null ? null : handler.TryCast<DownloadHandlerAudioClip>();
                if (audioHandler == null)
                    throw new InvalidOperationException(handler == null ? "UnityWebRequest returned a null DownloadHandler." : "UnityWebRequest DownloadHandler native type is not DownloadHandlerAudioClip.");

                audioHandler.streamAudio = false;
                audioHandler.compressed = false;
                streamAudioReadback = audioHandler.streamAudio;
                compressedReadback = audioHandler.compressed;
                if (streamAudioReadback != false || compressedReadback != false)
                    throw new InvalidOperationException("DownloadHandlerAudioClip did not accept non-streaming, decompressed mode.");

                req.disposeDownloadHandlerOnDispose = false;
                requestOwnsHandler = false;
                disposeFlagReadback = req.disposeDownloadHandlerOnDispose;
                if (disposeFlagReadback != false)
                {
                    requestOwnsHandler = true;
                    throw new InvalidOperationException("UnityWebRequest still owns its DownloadHandler after setting disposeDownloadHandlerOnDispose=false.");
                }

                var asyncOperation = req.SendWebRequest();
                while (!asyncOperation.isDone)
                {
                    if (isDestroyed) throw new ObjectDisposedException("RadioSound");
                    RecordExecutionContext("async-yield-before");
                    await Task.Yield();
                    RecordExecutionContext("async-yield-after");
                }
                if (req.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException("Failed to load audio file: " + req.error);

                handlerIsDone = handler.isDone;
                try
                {
                    var rawData = handler.data;
                    handlerDataBytes = rawData == null ? (int?)null : rawData.Length;
                    handlerDataStatus = rawData == null ? "null" : rawData.Length > 0 ? "available" : "empty";
                }
                catch (Exception dataError)
                {
                    handlerDataStatus = "unavailable";
                    handlerDataError = dataError.ToString();
                }
                RecordExecutionContext("before-get-content");
                sourceClip = DownloadHandlerAudioClip.GetContent(req);
                RecordExecutionContext("after-get-content");
                if (sourceClip == null)
                    throw new InvalidOperationException("DownloadHandlerAudioClip returned a null source clip.");

                RecordExecutionContext("before-load-audio-data");
                bool sourceLoadStarted = sourceClip.LoadAudioData();
                RecordExecutionContext("after-load-audio-data");
                if (!sourceLoadStarted && sourceClip.loadState.ToString() != "Loaded")
                    throw new InvalidOperationException("Downloaded AudioClip.LoadAudioData did not start: " + sourceClip.loadState);
                while (sourceClip.loadState.ToString() != "Loaded")
                {
                    if (isDestroyed) throw new ObjectDisposedException("RadioSound");
                    if (sourceClip.loadState.ToString() == "Failed") throw new InvalidOperationException("Downloaded AudioClip load failed.");
                    RecordExecutionContext("async-yield-before");
                    await Task.Yield();
                    RecordExecutionContext("async-yield-after");
                }
                if (sourceClip.loadState.ToString() != "Loaded")
                    throw new InvalidOperationException("Downloaded AudioClip did not reach Loaded state: " + sourceClip.loadState);

                sourceSamples = sourceClip.samples;
                sourceChannels = sourceClip.channels;
                sourceFrequency = sourceClip.frequency;
                sourceLength = sourceClip.length;
                sourceLoadType = sourceClip.loadType.ToString();
                if (handlerIsDone != true)
                    throw new InvalidOperationException("DownloadHandlerAudioClip is not complete.");
                if (sourceSamples.GetValueOrDefault() <= 0 || sourceChannels.GetValueOrDefault() <= 0 || sourceFrequency.GetValueOrDefault() <= 0 || sourceLength.GetValueOrDefault() <= 0)
                    throw new InvalidOperationException("Downloaded AudioClip has invalid PCM dimensions or zero native length.");
                if (!sourceClip.isReadyToPlay)
                    throw new InvalidOperationException("Downloaded AudioClip is Loaded but not ready to play.");
                if (string.Equals(sourceLoadType, "Streaming", StringComparison.Ordinal))
                    throw new InvalidOperationException("Streaming source clips cannot be copied through AudioClip.GetData.");

                int pcmValueCount = checked(sourceSamples.Value * sourceChannels.Value);
                var pcm = new Il2CppStructArray<float>(pcmValueCount);
                RecordExecutionContext("before-get-data");
                sourceGetDataSucceeded = sourceClip.GetData(pcm, 0);
                RecordExecutionContext("after-get-data");
                if (sourceGetDataSucceeded != true)
                    throw new InvalidOperationException("AudioClip.GetData returned false; refusing to publish a clip with unreadable PCM data.");

                float[] managedPcm = new float[pcmValueCount];
                for (int i = 0; i < pcmValueCount; i++) managedPcm[i] = pcm[i];

                sourceClipPointer = sourceClip.Pointer.ToString("X");
                playbackClip = CreateIndependentPcmPlaybackClip(sourceClip.Pointer, pcm,
                    sourceSamples.Value, sourceChannels.Value, sourceFrequency.Value,
                    out playbackSetDataSucceeded);
                if (playbackClip.loadState.ToString() != "Loaded")
                {
                    playbackLoadAudioDataCalls++; playbackLoadAudioDataFrame = Time.frameCount;
                    playbackClip.LoadAudioData();
                    int playbackLoadWaitFrames = 0;
                    while (playbackClip.loadState.ToString() != "Loaded" && playbackLoadWaitFrames < 120)
                    {
                        if (isDestroyed) throw new ObjectDisposedException("RadioSound");
                        if (playbackClip.loadState.ToString() == "Failed") throw new InvalidOperationException("Independent PCM AudioClip load failed.");
                        RecordExecutionContext("pcm-copy-load-yield-before");
                        await Task.Yield();
                        RecordExecutionContext("pcm-copy-load-yield-after");
                        playbackLoadWaitFrames++;
                    }
                    if (playbackClip.loadState.ToString() != "Loaded") throw new TimeoutException("Independent PCM AudioClip did not reach Loaded within 120 owner frames.");
                }
                if (!playbackClip.isReadyToPlay) throw new InvalidOperationException("Independent PCM AudioClip is Loaded but not ready to play.");
                var playbackVerificationPcm = new Il2CppStructArray<float>(pcmValueCount);
                playbackGetDataSucceeded = playbackClip.GetData(playbackVerificationPcm, 0);
                if (playbackGetDataSucceeded != true) throw new InvalidOperationException("Independent PCM AudioClip.GetData readback returned false.");
                playbackStrategy = "independent-create-setdata-pcm-copy";
                playbackIsDownloadedSource = false;
                playbackClipPointer = playbackClip.Pointer.ToString("X");
                playbackSamples = playbackClip.samples;
                playbackChannels = playbackClip.channels;
                playbackFrequency = playbackClip.frequency;
                playbackLength = playbackClip.length;
                playbackLoadType = playbackClip.loadType.ToString();
                if (isDestroyed || !PinOwnedAudioClip(playbackClip))
                    throw new ObjectDisposedException("RadioSound");
                playbackClipPinned = true;
                if (isDestroyed || !PinOwnedAudioClip(sourceClip))
                    throw new ObjectDisposedException("RadioSound");
                sourceClipPinned = true;
                if (isDestroyed || !RetainOwnedDownloadHandler(handler))
                    throw new ObjectDisposedException("RadioSound");
                handlerRetained = true;
                cachedPcmBySlot[slotKey] = new CachedPcmAudio(managedPcm, sourceSamples.Value, sourceChannels.Value, sourceFrequency.Value, sourceClip.Pointer, Path.GetFileName(uri));
                playbackHideFlags = playbackClip.hideFlags.ToString();
                return playbackClip;
            }
            catch (Exception error)
            {
                failure = error.ToString();
                clipLoadDiagnosticError = failure;
                throw;
            }
            finally
            {
                if (handler != null && !requestOwnsHandler && !handlerRetained)
                {
                    try { handler.Dispose(); handlerDisposed = true; }
                    catch (Exception error) { if (failure == null) failure = error.ToString(); clipLoadDiagnosticError = error.ToString(); try { req.disposeDownloadHandlerOnDispose = true; requestOwnsHandler = true; } catch (Exception fallbackError) { clipLoadDiagnosticError = fallbackError.ToString(); } }
                }
                if (!handlerRetained)
                {
                    if (playbackClip != null) ReleaseOwnedAudioClip(playbackClip, true, "load-failure");
                    if (sourceClip != null) ReleaseOwnedAudioClip(sourceClip, true, "load-failure");
                }
                try { req.Dispose(); requestDisposed = true; if (handler != null && requestOwnsHandler) handlerDisposed = true; }
                catch (Exception error) { if (failure == null) failure = error.ToString(); clipLoadDiagnosticError = error.ToString(); }
                RecordClipLoad(uri, disposeFlagReadback, streamAudioReadback, compressedReadback,
                    handlerIsDone, handlerDataBytes, handlerDataStatus, handlerDataError, sourceLength, sourceSamples, sourceChannels, sourceFrequency, sourceLoadType,
                    sourceGetDataSucceeded, playbackLength, playbackSamples, playbackChannels, playbackFrequency, playbackLoadType, playbackHideFlags,
                    playbackStrategy, sourceClipPointer, playbackClipPointer, playbackSetDataSucceeded, playbackGetDataSucceeded, playbackIsDownloadedSource, sourceClipPinned, handlerRetained, handlerDisposed, requestDisposed, playbackClipPinned, playbackClip, failure);
            }
        }

        [HideFromIl2Cpp]
        public async void PlayRadioBeep()
        {
            try
            {
                float level = (pitFireTeam.pingRadioVolume.Value / 100f) * 0.3f;
                // Lazy load when async Enable() has not completed yet.
                if (radioBeepClip == null || radioBeepClip.length == 0)
                {
                    if (audioConfigurationEpoch != audioRecoveryHandledEpoch) { RecordPlaybackDispatch("PlayRadioBeep", "deferred-audio-config-recovery", radioBeepClip, false, null, null); return; }
                    long generation = audioRecoveryGeneration;
                    AudioClip loaded = await LoadAudioClip("file://" + ResolveSoundPath("radiobeep.ogg"));
                    if (isDestroyed || generation != audioRecoveryGeneration || !ReferenceEquals(currentOwner, this)) { ReleaseOwnedAudioClip(loaded, true, "stale-audio-generation"); return; }
                    ReplaceOwnedAudioClip(ref radioBeepClip, loaded);
                }

                if (radioBeepClip == null) { RecordPlaybackDispatch("PlayRadioBeep", "clip-unavailable", radioBeepClip, false, null, null); return; }
                PlayGuiSoundAndRecord("PlayRadioBeep", "GUISounds.PlaySound", radioBeepClip, level);
            }
            catch (System.Exception e)
            {
                RecordPlaybackDispatch("PlayRadioBeep", "consumer-exception", radioBeepClip, false, null, e.ToString());
                Logger.LogError(e);
            }
        }

        [HideFromIl2Cpp]
        public async void PlayRadioSound(Vector3 worldPosition)
        {
            try
            {
                float level = (pitFireTeam.pingRadioVolume.Value / 100f) * 0.5f;
                if (radioClip == null || radioClip.length == 0)
                {
                    if (audioConfigurationEpoch != audioRecoveryHandledEpoch) { RecordPlaybackDispatch("PlayRadioSound", "deferred-audio-config-recovery", radioClip, false, null, null); return; }
                    long generation = audioRecoveryGeneration;
                    AudioClip loaded = await LoadAudioClip("file://" + ResolveSoundPath("radiochat.ogg"));
                    if (isDestroyed || generation != audioRecoveryGeneration || !ReferenceEquals(currentOwner, this)) { ReleaseOwnedAudioClip(loaded, true, "stale-audio-generation"); return; }
                    ReplaceOwnedAudioClip(ref radioClip, loaded);
                }

                if (radioClip == null) { RecordPlaybackDispatch("PlayRadioSound", "clip-unavailable", radioClip, false, null, null); return; }

                try
                {
                    BetterAudio audio = Singleton<BetterAudio>.Instance;
                    if (audio == null)
                    {
                        PlayGuiSoundAndRecord("PlayRadioSound", "GUISounds.PlaySound:BetterAudio-unavailable", radioClip, level);
                        return;
                    }

                    PlayOptions options = PlayOptions.Default;
                    options.volume = level;
                    options.occlusionTest = EOcclusionTest.None;
                    BetterSource source = audio.PlayAtPoint(
                        worldPosition,
                        radioClip,
                        BetterAudio.AudioSourceGroupType.Speech,
                        90,
                        ref options);
                    RecordPlaybackDispatch("PlayRadioSound", "BetterAudio.PlayAtPoint", radioClip, true, source, null);

                    if (source == null)
                    {
                        PlayGuiSoundAndRecord("PlayRadioSound", "GUISounds.PlaySound:BetterAudio-null-source", radioClip, level);
                    }
                }
                catch (Exception betterAudioError)
                {
                    RecordPlaybackDispatch("PlayRadioSound", "BetterAudio.PlayAtPoint", radioClip, false, null, betterAudioError.ToString());
                    PlayGuiSoundAndRecord("PlayRadioSound", "GUISounds.PlaySound:BetterAudio-exception-fallback", radioClip, level);
                }
            }
            catch (System.Exception e)
            {
                RecordPlaybackDispatch("PlayRadioSound", "consumer-exception", radioClip, false, null, e.ToString());
                Logger.LogError(e);
            }
        }

        [HideFromIl2Cpp]
        public async void PlayLocationSound(Vector3 worldPosition)
        {
            try
            {
                float level = (pitFireTeam.statusSound.Value / 100f) * 0.6f;
                // Lazy load when async Enable() has not completed yet.
                if (locationClip == null || locationClip.length == 0)
                {
                    if (audioConfigurationEpoch != audioRecoveryHandledEpoch) { RecordPlaybackDispatch("PlayLocationSound", "deferred-audio-config-recovery", locationClip, false, null, null); return; }
                    long generation = audioRecoveryGeneration;
                    AudioClip loaded = await LoadAudioClip("file://" + ResolveSoundPath("locationping.ogg"));
                    if (isDestroyed || generation != audioRecoveryGeneration || !ReferenceEquals(currentOwner, this)) { ReleaseOwnedAudioClip(loaded, true, "stale-audio-generation"); return; }
                    ReplaceOwnedAudioClip(ref locationClip, loaded);
                }

                if (locationClip == null) { RecordPlaybackDispatch("PlayLocationSound", "clip-unavailable", locationClip, false, null, null); return; }

                try
                {
                    BetterAudio audio = Singleton<BetterAudio>.Instance;
                    if (audio == null)
                    {
                        PlayGuiSoundAndRecord("PlayLocationSound", "GUISounds.PlaySound:BetterAudio-unavailable", locationClip, level);
                        return;
                    }

                    PlayOptions options = PlayOptions.Default;
                    options.volume = level;
                    options.occlusionTest = EOcclusionTest.None;
                    BetterSource source = audio.PlayAtPoint(
                        worldPosition,
                        locationClip,
                        BetterAudio.AudioSourceGroupType.Speech,
                        90,
                        ref options);
                    RecordPlaybackDispatch("PlayLocationSound", "BetterAudio.PlayAtPoint", locationClip, true, source, null);

                    // BetterAudio can fail silently (null source) if distance/rolloff checks fail.
                    if (source == null)
                    {
                        PlayGuiSoundAndRecord("PlayLocationSound", "GUISounds.PlaySound:BetterAudio-null-source", locationClip, level);
                    }
                }
                catch (Exception betterAudioError)
                {
                    RecordPlaybackDispatch("PlayLocationSound", "BetterAudio.PlayAtPoint", locationClip, false, null, betterAudioError.ToString());
                    PlayGuiSoundAndRecord("PlayLocationSound", "GUISounds.PlaySound:BetterAudio-exception-fallback", locationClip, level);
                }
            }
            catch (System.Exception e)
            {
                RecordPlaybackDispatch("PlayLocationSound", "consumer-exception", locationClip, false, null, e.ToString());
                Logger.LogError(e);
            }
        }

        [HideFromIl2Cpp]
        private static string ResolveSoundPath(string fileName)
        {
            string dllDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dllDir))
            {
                throw new Exception("Cannot resolve plugin directory for sound loading.");
            }

            // Prefer root next to DLL, then resources subfolder.
            string direct = Path.Combine(dllDir, fileName);
            if (File.Exists(direct)) return direct;

            string resources = Path.Combine(dllDir, "resources", fileName);
            if (File.Exists(resources)) return resources;

            throw new FileNotFoundException($"Sound file not found: {fileName}. Checked: {direct} and {resources}");
        }
    }

}
