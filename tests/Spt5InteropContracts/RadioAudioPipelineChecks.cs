using System.Text.Json;
using Mono.Cecil;
internal static class RadioAudioPipelineChecks
{
    private readonly record struct ReceiptClip(bool Alive, string LoadState, bool UnityReady, float Length);

    public static int Run(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("source root required");
        string sourceRoot = Path.GetFullPath(args[0]);
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "radio-audio-pipeline-cases.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        JsonElement root = fixture.RootElement;
        string positivePath = root.GetProperty("afPositiveEvidencePath").GetString()!;
        string zeroPath = root.GetProperty("afZeroEvidencePath").GetString()!;
        ReceiptClip positive = ReadActualClip(positivePath);
        ReceiptClip zero = ReadActualClip(zeroPath);
        bool actualAfPositiveShape = positive.Alive && positive.LoadState == "Loaded" && positive.UnityReady && positive.Length > 0;
        bool actualAfZeroShape = zero.Alive && zero.LoadState == "Loaded" && zero.UnityReady && zero.Length == 0;
        string agFailurePath = root.GetProperty("agCastFailureEvidencePath").GetString()!;
        string agFailureMessage = root.GetProperty("agCastFailureExpectedMessage").GetString()!;
        bool expectedAgOwnerAlive = root.GetProperty("agCastFailureOwnerExpectedAlive").GetBoolean();
        bool actualAgCastFailureShape = ReadActualAgCastFailure(agFailurePath, agFailureMessage, expectedAgOwnerAlive);
        string aiZeroPath = root.GetProperty("aiZeroTransitionEvidencePath").GetString()!;
        string ahZeroPath = root.GetProperty("ahZeroTransitionEvidencePath").GetString()!;
        bool actualAiZeroTransition = ReadActualZeroTransition(aiZeroPath, true);
        bool actualAhZeroTransition = ReadActualZeroTransition(ahZeroPath, false);
        int amZeroEvidenceCount = 0;
        bool amZeroEvidencePassed = true;
        foreach (JsonElement item in root.GetProperty("amSamePointerZeroEvidencePaths").EnumerateArray())
        {
            amZeroEvidenceCount++;
            amZeroEvidencePassed &= ReadActualAmSamePointerZero(item.GetString()!);
        }
        bool actualAmSamePointerZero = amZeroEvidenceCount == 3 && amZeroEvidencePassed;
        string aqZeroPath = root.GetProperty("aqSamePointerZeroEvidencePath").GetString()!;
        bool actualAqSamePointerZero = ReadActualAqSamePointerZero(aqZeroPath);
        string arResetPath = root.GetProperty("arAudioConfigurationLossEvidencePath").GetString()!;
        bool actualArConfigurationLoss = ReadActualArConfigurationLoss(arResetPath);
        var recoveryPolicyCases = new[]
        {
            (name: "actual-ar-reset-loss-rebuilds", passed: actualArConfigurationLoss && pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(true, true, true, true, 17708, 17708, 0, 3)),
            (name: "inactive-owner-rejected", passed: !pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(false, true, true, true, 17708, 17708, 0, 3)),
            (name: "no-reset-pending-rejected", passed: !pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(true, false, true, true, 17708, 17708, 0, 3)),
            (name: "healthy-playback-rejected", passed: !pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(true, true, false, true, 17708, 17708, 0, 3)),
            (name: "incomplete-pcm-cache-rejected", passed: !pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(true, true, true, false, 17708, 17708, 0, 3)),
            (name: "same-frame-reset-rejected", passed: !pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(true, true, true, true, 17707, 17708, 0, 3)),
            (name: "retry-limit-rejected", passed: !pitTeam.Compat.RadioAudioRecoveryPolicy.ShouldRebuild(true, true, true, true, 17738, 17708, 3, 3))
        };
        bool recoveryPolicyCasesPassed = recoveryPolicyCases.All(item => item.passed);
        string aoGuardFailurePath = root.GetProperty("aoPcmGuardFailureEvidencePath").GetString()!;
        bool actualAoGuardFailureShape = ReadActualAoPcmGuardFailure(aoGuardFailurePath);
        string apDurationRaw = File.ReadAllText(root.GetProperty("apDurationQuantizationEvidencePath").GetString()!);
        bool actualApDurationQuantization = apDurationRaw.Contains("playbackPointer=1925C769CF0", StringComparison.Ordinal) && apDurationRaw.Contains("samplesMatch=True", StringComparison.Ordinal) && apDurationRaw.Contains("channelsMatch=True", StringComparison.Ordinal) && apDurationRaw.Contains("frequencyMatches=True", StringComparison.Ordinal) && apDurationRaw.Contains("durationMatches=False", StringComparison.Ordinal) && apDurationRaw.Contains("loadState=Loaded", StringComparison.Ordinal) && apDurationRaw.Contains("loadType=DecompressOnLoad", StringComparison.Ordinal);
        bool actualHandlerInterop = ReadHandlerInterop(root.GetProperty("interopRoot").GetString()!);
        bool actualPlaybackInterop = ReadPlaybackInterop(root.GetProperty("interopRoot").GetString()!, root.GetProperty("normalPlaybackReceipt"));

        var predicateCases = new List<object>();
        bool predicateCasesPassed = true;
        foreach (JsonElement item in root.GetProperty("predicateCases").EnumerateArray())
        {
            string name = item.GetProperty("name").GetString()!;
            string receipt = item.TryGetProperty("receipt", out var receiptValue) ? receiptValue.GetString()! : "";
            ReceiptClip observed = receipt == "afPositive" ? positive : receipt == "afZero" ? zero : default;
            float length = receipt.Length == 0 ? item.GetProperty("length").GetSingle() : observed.Length;
            bool alive = receipt.Length == 0 ? item.GetProperty("alive").GetBoolean() : observed.Alive;
            string loadState = receipt.Length == 0 ? item.GetProperty("loadState").GetString()! : observed.LoadState;
            bool unityReady = receipt.Length == 0 ? item.GetProperty("unityReady").GetBoolean() : observed.UnityReady;
            int samples = item.GetProperty("samples").GetInt32();
            int channels = item.GetProperty("channels").GetInt32();
            int frequency = item.GetProperty("frequency").GetInt32();
            string loadType = item.GetProperty("loadType").GetString()!;
            bool expected = item.GetProperty("expectedReady").GetBoolean();
            bool actual = StrictReady(alive, loadState, unityReady, length, samples, channels, frequency, loadType);
            bool passed = actual == expected;
            predicateCasesPassed &= passed;
            predicateCases.Add(new { name, receipt, length, samples, channels, frequency, loadType, expected, actual, passed,
                supplementalFormatIsSynthetic = item.GetProperty("supplementalFormatIsSynthetic").GetBoolean() });
        }

        bool pcmPlaybackDurationCasesPassed = true;
        var pcmPlaybackDurationCases = new List<object>();
        foreach (JsonElement item in root.GetProperty("pcmPlaybackDurationCases").EnumerateArray())
        {
            int ss = item.GetProperty("sourceSamples").GetInt32(), sc = item.GetProperty("sourceChannels").GetInt32(), sf = item.GetProperty("sourceFrequency").GetInt32();
            int ps = item.GetProperty("playbackSamples").GetInt32(), pc = item.GetProperty("playbackChannels").GetInt32(), pf = item.GetProperty("playbackFrequency").GetInt32();
            float pl = item.GetProperty("playbackLength").GetSingle();
            bool durationMatches = pitTeam.Compat.RadioAudioDurationPolicy.MatchesPcmDuration(ss, sf, pl, out float expectedPcmLength, out float tolerance);
            bool accepted = item.GetProperty("pointerIndependent").GetBoolean() && sc > 0 && ps == ss && pc == sc && pf == sf && durationMatches;
            bool expected = item.GetProperty("expectedAccepted").GetBoolean(), passed = accepted == expected;
            pcmPlaybackDurationCasesPassed &= passed;
            pcmPlaybackDurationCases.Add(new { name = item.GetProperty("name").GetString(), expected, accepted, passed, sourceLength = item.GetProperty("sourceLength").GetSingle(), expectedPcmLength, pl, tolerance });
        }

        bool invalidDurationCasesPassed = !pitTeam.Compat.RadioAudioDurationPolicy.MatchesPcmDuration(101025, 44100, 0f, out _, out _) && !pitTeam.Compat.RadioAudioDurationPolicy.MatchesPcmDuration(101025, 44100, float.NaN, out _, out _) && !pitTeam.Compat.RadioAudioDurationPolicy.MatchesPcmDuration(101025, 44100, float.PositiveInfinity, out _, out _) && !pitTeam.Compat.RadioAudioDurationPolicy.MatchesPcmDuration(22080, 44100, 0.502f, out _, out _);
        string radioPath = Path.Combine(sourceRoot, "client", "Modules", "RadioSound.cs");
        string worldPath = Path.Combine(sourceRoot, "client", "Compat", "Spt5RuntimeWorldProbe.cs");
        string radioSource = File.ReadAllText(radioPath);
        string worldSource = File.ReadAllText(worldPath);
        bool audioRecoveryWiring = radioSource.Contains("TryRecoverAfterAudioConfigurationChange()", StringComparison.Ordinal) && radioSource.Contains("CreateInteropPcm(cached.Samples)", StringComparison.Ordinal) && radioSource.Contains("RadioAudioRecoveryPolicy.ShouldRebuild", StringComparison.Ordinal) && radioSource.Contains("generation != audioRecoveryGeneration", StringComparison.Ordinal) && radioSource.Contains("AudioRecoveryMaxAttempts = 3", StringComparison.Ordinal) && radioSource.Contains("AudioRecoveryRetryDelayFrames = 30", StringComparison.Ordinal) && radioSource.Contains("recovered-from-managed-pcm", StringComparison.Ordinal);
        bool aoGuardDiagnosticWiring = radioSource.Contains("pointerIndependent=", StringComparison.Ordinal)
            && radioSource.Contains("samplesMatch=", StringComparison.Ordinal) && radioSource.Contains("channelsMatch=", StringComparison.Ordinal)
            && radioSource.Contains("frequencyMatches=", StringComparison.Ordinal) && radioSource.Contains("durationMatches=", StringComparison.Ordinal)
            && radioSource.Contains("loadState=", StringComparison.Ordinal) && radioSource.Contains("loadType=", StringComparison.Ordinal);
        int loadStart = radioSource.IndexOf("private async Task<AudioClip> LoadAudioClip", StringComparison.Ordinal);
        int loadEnd = loadStart < 0 ? -1 : radioSource.IndexOf("public async void PlayRadioBeep", loadStart, StringComparison.Ordinal);
        string loader = loadStart >= 0 && loadEnd > loadStart ? radioSource.Substring(loadStart, loadEnd - loadStart) : "";
        int getData = loader.IndexOf("sourceClip.GetData(pcm, 0)", StringComparison.Ordinal);
        int getContent = loader.IndexOf("DownloadHandlerAudioClip.GetContent(req)", StringComparison.Ordinal);
        int independentCopy = loader.IndexOf("CreateIndependentPcmPlaybackClip(sourceClip.Pointer, pcm", StringComparison.Ordinal);
        int pinPlayback = loader.IndexOf("PinOwnedAudioClip(playbackClip)", StringComparison.Ordinal);
        int pinSource = loader.IndexOf("PinOwnedAudioClip(sourceClip)", StringComparison.Ordinal);
        int retainOwnedHandler = loader.IndexOf("RetainOwnedDownloadHandler(handler)", StringComparison.Ordinal);
        int retainHandler = loader.IndexOf("handlerRetained = true;", StringComparison.Ordinal);
        int returnDirect = loader.IndexOf("return playbackClip;", StringComparison.Ordinal);
        bool nativeHandlerCastWiring = loader.Contains("handler.TryCast<DownloadHandlerAudioClip>()", StringComparison.Ordinal)
            && !loader.Contains("handler as DownloadHandlerAudioClip", StringComparison.Ordinal)
            && loader.Contains("UnityWebRequest DownloadHandler native type is not DownloadHandlerAudioClip.", StringComparison.Ordinal);
        int handlerDataRead = loader.IndexOf("var rawData = handler.data", StringComparison.Ordinal);
        int handlerDataFailureCatch = handlerDataRead < 0 ? -1 : loader.IndexOf("catch (Exception dataError)", handlerDataRead, StringComparison.Ordinal);
        string dataFailureBlock = handlerDataFailureCatch >= 0 && getContent > handlerDataFailureCatch ? loader.Substring(handlerDataFailureCatch, getContent - handlerDataFailureCatch) : "";
        bool optionalHandlerDataDiagnosticWiring = handlerDataRead >= 0 && handlerDataFailureCatch > handlerDataRead && getContent > handlerDataFailureCatch
            && dataFailureBlock.Contains("handlerDataStatus = ", StringComparison.Ordinal)
            && dataFailureBlock.Contains("handlerDataError = dataError.ToString()", StringComparison.Ordinal)
            && !dataFailureBlock.Contains("throw", StringComparison.Ordinal)
            && radioSource.Contains("handlerDataStatus, handlerDataError", StringComparison.Ordinal);
        bool normalLoaderWiring = loadStart >= 0 && loadEnd > loadStart && getContent >= 0 && getData > getContent
            && independentCopy > getData && pinPlayback > independentCopy && pinSource > pinPlayback && retainOwnedHandler > pinSource && retainHandler > retainOwnedHandler && returnDirect > retainHandler
            && loader.Contains("audioHandler.streamAudio = false;", StringComparison.Ordinal)
            && loader.Contains("audioHandler.compressed = false;", StringComparison.Ordinal)
            && loader.Contains("handler.isDone", StringComparison.Ordinal)
            && loader.Contains("req.disposeDownloadHandlerOnDispose = false;", StringComparison.Ordinal)
            && loader.Contains("sourceClip.samples", StringComparison.Ordinal) && loader.Contains("sourceClip.channels", StringComparison.Ordinal)
            && loader.Contains("sourceClip.frequency", StringComparison.Ordinal) && loader.Contains("sourceClip.length", StringComparison.Ordinal)
            && loader.Contains("sourceClip.isReadyToPlay", StringComparison.Ordinal)
            && loader.Contains("handlerRetained = true;", StringComparison.Ordinal)
            && loader.Contains("!handlerRetained", StringComparison.Ordinal)
            && loader.Contains("req.Dispose()", StringComparison.Ordinal)
            && loader.Contains("AudioClip.GetData returned false", StringComparison.Ordinal)
            && !loader.Contains("playbackClip = sourceClip;", StringComparison.Ordinal)
            && radioSource.Contains("AudioClip.Create(", StringComparison.Ordinal)
            && radioSource.Contains("playbackClip.SetData(pcm, 0)", StringComparison.Ordinal)
            && radioSource.Contains("playbackClip.GetData(playbackVerificationPcm, 0)", StringComparison.Ordinal)
            && loader.Contains("sourceClip.Pointer", StringComparison.Ordinal)
            && loader.Contains("sourceClipPinned = true;", StringComparison.Ordinal)
            && loader.Contains("playbackSetDataSucceeded", StringComparison.Ordinal)
            && loader.Contains("playbackClip.LoadAudioData()", StringComparison.Ordinal)
            && loader.Contains("playbackLoadWaitFrames < 120", StringComparison.Ordinal)
            && loader.Contains("await Task.Yield();", StringComparison.Ordinal)
            && loader.Contains("playbackClip.isReadyToPlay", StringComparison.Ordinal)
            && radioSource.Contains("playbackClip.Pointer == sourcePointer", StringComparison.Ordinal)
            && radioSource.Contains("playbackClip.samples != sourceSamples", StringComparison.Ordinal)
            && radioSource.Contains("playbackClip.channels != sourceChannels", StringComparison.Ordinal)
            && radioSource.Contains("playbackClip.frequency != sourceFrequency", StringComparison.Ordinal)
            && radioSource.Contains("RadioAudioDurationPolicy.MatchesPcmDuration(sourceSamples, sourceFrequency, playbackLength", StringComparison.Ordinal)
            && File.ReadAllText(Path.Combine(sourceRoot, "client", "Compat", "RadioAudioDurationPolicy.cs")).Contains("0.001f + (1f / frequency)", StringComparison.Ordinal)
            && !radioSource.Contains("playbackClip.length != sourceLength", StringComparison.Ordinal);
        bool pairedHandlerOwnershipWiring = radioSource.Contains("ownedDownloadHandlers.Add(handler)", StringComparison.Ordinal)
            && radioSource.Contains("DownloadHandler handler = ownedDownloadHandlers[i];", StringComparison.Ordinal)
            && !radioSource.Contains("ownedDownloadHandlers.RemoveAt(i);", StringComparison.Ordinal)
            && radioSource.Contains("ReleaseAllOwnedAudioClips();", StringComparison.Ordinal)
            && radioSource.Contains("finally { DisposeOwnedDownloadHandlers(); }", StringComparison.Ordinal)
            && radioSource.Contains("sourceClipPinned", StringComparison.Ordinal)
            && radioSource.Contains("playbackClipPinned", StringComparison.Ordinal)
            && radioSource.Contains("playbackClipPointer", StringComparison.Ordinal)
            && radioSource.Contains("sourcePointer = sourceClipPointer", StringComparison.Ordinal)
            && radioSource.Contains("setDataSucceeded = playbackSetDataSucceeded", StringComparison.Ordinal)
            && radioSource.Contains("getDataSucceeded = playbackGetDataSucceeded", StringComparison.Ordinal)
            && radioSource.Contains("afterRequestDispose = new", StringComparison.Ordinal)
            && radioSource.Contains("handlerRetained, handlerDisposed, requestDisposed", StringComparison.Ordinal)
            && radioSource.Contains("isDownloadedSource = playbackIsDownloadedSource", StringComparison.Ordinal);
        bool ownershipDiagnosticsWiring = radioSource.Contains("ObserveOwnedClipDimensions()", StringComparison.Ordinal) && radioSource.Contains("clipDimensionLossDiagnostics", StringComparison.Ordinal) && radioSource.Contains("RegisterAudioConfigurationObserver()", StringComparison.Ordinal) && radioSource.Contains("audioConfigurationChangeCount", StringComparison.Ordinal) && radioSource.Contains("playbackLoadAudioDataCalls", StringComparison.Ordinal) && radioSource.Contains("ownedClipReleaseCount", StringComparison.Ordinal) && radioSource.Contains("lastOwnedClipReleaseReason", StringComparison.Ordinal);
        bool normalPlaybackReceiptWiring = radioSource.Contains("RecordPlaybackDispatch", StringComparison.Ordinal) && radioSource.Contains("playbackDispatchDiagnostics", StringComparison.Ordinal) && radioSource.Contains("not-observed-void-return", StringComparison.Ordinal) && radioSource.Contains("BetterAudio.PlayAtPoint", StringComparison.Ordinal) && radioSource.Contains("GUISounds.PlaySound", StringComparison.Ordinal) && radioSource.Contains("playbackDispatches", StringComparison.Ordinal) && new[] { "PlayRadioBeep", "PlayRadioSound", "PlayLocationSound" }.All(name => radioSource.Contains(name, StringComparison.Ordinal));
        bool strictProbeWiring = worldSource.Contains("unityReadyToPlay == true", StringComparison.Ordinal)
            && worldSource.Contains("length.GetValueOrDefault() > 0", StringComparison.Ordinal)
            && worldSource.Contains("samples.GetValueOrDefault() > 0", StringComparison.Ordinal)
            && worldSource.Contains("channels.GetValueOrDefault() > 0", StringComparison.Ordinal)
            && worldSource.Contains("frequency.GetValueOrDefault() > 0", StringComparison.Ordinal)
            && worldSource.Contains("loaded-content-unavailable-or-invalid", StringComparison.Ordinal)
            && worldSource.Contains("healthStatus", StringComparison.Ordinal);
        bool passedAll = actualAfPositiveShape && actualAfZeroShape && actualAgCastFailureShape && actualAiZeroTransition && actualAhZeroTransition && actualAmSamePointerZero && actualAqSamePointerZero && actualArConfigurationLoss && recoveryPolicyCasesPassed && audioRecoveryWiring && actualAoGuardFailureShape && actualApDurationQuantization && aoGuardDiagnosticWiring && actualHandlerInterop && actualPlaybackInterop && normalPlaybackReceiptWiring && predicateCasesPassed && pcmPlaybackDurationCasesPassed && invalidDurationCasesPassed && nativeHandlerCastWiring && optionalHandlerDataDiagnosticWiring && normalLoaderWiring && pairedHandlerOwnershipWiring && ownershipDiagnosticsWiring && strictProbeWiring;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            test = "radio-audio-pipeline-self-test",
            passed = passedAll,
            fixturePath,
            actualAfPositiveEvidencePath = positivePath,
            actualAfZeroEvidencePath = zeroPath,
            actualAfPositiveShape,
            actualAfZeroShape,
            agCastFailurePath = agFailurePath,
            actualAgCastFailureShape,
            aiZeroTransitionEvidencePath = aiZeroPath,
            actualAiZeroTransition,
            ahZeroTransitionEvidencePath = ahZeroPath,
            actualAhZeroTransition,
            actualAmSamePointerZero,
            aqZeroEvidencePath = aqZeroPath,
            actualAqSamePointerZero,
            arResetEvidencePath = arResetPath,
            unityAudioConfigurationDocsUrl = root.GetProperty("unityAudioConfigurationDocsUrl").GetString(),
            actualArConfigurationLoss,
            recoveryPolicyCases,
            recoveryPolicyCasesPassed,
            audioRecoveryWiring,
            ownershipDiagnosticsWiring,
            apDurationEvidencePath = root.GetProperty("apDurationQuantizationEvidencePath").GetString(),
            actualApDurationQuantization,
            aoGuardFailurePath,
            actualAoGuardFailureShape,
            aoGuardDiagnosticWiring,
            actualHandlerInterop,
            actualPlaybackInterop,
            normalPlaybackReceiptWiring,
            nativeHandlerCastWiring,
            optionalHandlerDataDiagnosticWiring,
            predicateCases = predicateCases.ToArray(),
            pcmPlaybackDurationCasesPassed,
            invalidDurationCasesPassed,
            pcmPlaybackDurationCases = pcmPlaybackDurationCases.ToArray(),
            normalLoaderWiring,
            strictProbeWiring
        }));
        return passedAll ? 0 : 1;
    }

    private static bool ReadActualArConfigurationLoss(string path)
    {
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement owner = evidence.RootElement.GetProperty("result").GetProperty("radioSoundInitialization").GetProperty("owner");
        JsonElement load = owner.GetProperty("loadDiagnostics");
        JsonElement change = load.GetProperty("lastAudioConfigurationChange");
        JsonElement losses = load.GetProperty("clipDimensionLossDiagnostics");
        if (!owner.GetProperty("unityObjectAlive").GetBoolean() || !owner.GetProperty("gameObjectAlive").GetBoolean() || load.GetProperty("audioConfigurationChangeCount").GetInt32() != 1
            || change.GetProperty("frame").GetInt32() != 17707 || change.GetProperty("threadId").GetInt32() != 1 || change.GetProperty("deviceWasChanged").GetBoolean()
            || load.GetProperty("playbackLoadAudioDataCalls").GetInt32() != 0 || load.GetProperty("ownedClipReleaseCount").GetInt32() != 0
            || load.GetProperty("error").ValueKind != JsonValueKind.Null || losses.GetArrayLength() != 6) return false;
        foreach (JsonElement item in losses.EnumerateArray())
            if (item.GetProperty("frame").GetInt32() != 17708 || item.GetProperty("length").GetSingle() != 0 || item.GetProperty("samples").GetInt32() != 0 || item.GetProperty("channels").GetInt32() != 0 || item.GetProperty("loadState").GetString() != "Loaded") return false;
        return true;
    }
    private static bool ReadActualAqSamePointerZero(string path)
    {
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement radio = evidence.RootElement.GetProperty("result").GetProperty("radioSoundInitialization");
        JsonElement owner = radio.GetProperty("owner");
        JsonElement events = owner.GetProperty("loadDiagnostics").GetProperty("events");
        if (radio.GetProperty("task").GetProperty("status").GetString() != "RanToCompletion" || !owner.GetProperty("unityObjectAlive").GetBoolean() || !owner.GetProperty("gameObjectAlive").GetBoolean()) return false;
        string[] fields = { "radioClip", "locationClip", "radioBeepClip" }; string[] names = { "radiochat.ogg", "locationping.ogg", "radiobeep.ogg" };
        for (int i = 0; i < fields.Length; i++)
        {
            JsonElement clip = radio.GetProperty(fields[i]); JsonElement load = events.EnumerateArray().FirstOrDefault(x => x.GetProperty("fileName").GetString() == names[i]); if (load.ValueKind != JsonValueKind.Object) return false;
            JsonElement playback = load.GetProperty("playback"); JsonElement after = load.GetProperty("afterHandlerAndRequestDispose"); string pointer = clip.GetProperty("pointer").GetString()!;
            if (playback.GetProperty("strategy").GetString() != "independent-create-setdata-pcm-copy" || playback.GetProperty("pointer").GetString() != pointer || playback.GetProperty("sourcePointer").GetString() == pointer || load.GetProperty("playbackPointerAfterRequestDispose").GetString() != pointer || !playback.GetProperty("setDataSucceeded").GetBoolean() || !playback.GetProperty("getDataSucceeded").GetBoolean() || after.GetProperty("length").GetSingle() <= 0 || after.GetProperty("samples").GetInt32() <= 0 || after.GetProperty("channels").GetInt32() <= 0 || after.GetProperty("frequency").GetInt32() <= 0 || !load.GetProperty("requestDisposed").GetBoolean() || !clip.GetProperty("unityObjectAlive").GetBoolean() || clip.GetProperty("loadState").GetString() != "Loaded" || clip.GetProperty("length").GetSingle() != 0 || clip.GetProperty("samples").GetInt32() != 0 || clip.GetProperty("channels").GetInt32() != 0 || clip.GetProperty("frequency").GetInt32() <= 0) return false;
        }
        return true;
    }
    private static bool ReadActualZeroTransition(string path, bool requirePointerMatch)
    {
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement radio = evidence.RootElement.GetProperty("result").GetProperty("radioSoundInitialization");
        JsonElement owner = radio.GetProperty("owner");
        JsonElement events = owner.GetProperty("loadDiagnostics").GetProperty("events");
        JsonElement first = events.EnumerateArray().FirstOrDefault(x => x.GetProperty("fileName").GetString() == "radiochat.ogg");
        if (first.ValueKind != JsonValueKind.Object) return false;
        JsonElement clip = radio.GetProperty("radioClip");
        JsonElement playback = first.GetProperty("playback");
        bool pointerMatch = first.TryGetProperty("playbackPointerAfterRequestDispose", out JsonElement oldPointer) && oldPointer.GetString() == clip.GetProperty("pointer").GetString();
        return (!requirePointerMatch || pointerMatch)
            && radio.GetProperty("task").GetProperty("status").GetString() == "RanToCompletion"
            && owner.GetProperty("unityObjectAlive").GetBoolean() && owner.GetProperty("gameObjectAlive").GetBoolean()
            && clip.GetProperty("unityObjectAlive").GetBoolean() && clip.GetProperty("loadState").GetString() == "Loaded"
            && !clip.GetProperty("isReadyToPlay").GetBoolean() && clip.GetProperty("length").GetSingle() == 0
            && clip.GetProperty("samples").GetInt32() == 0 && clip.GetProperty("channels").GetInt32() == 0
            && playback.GetProperty("length").GetSingle() > 0 && playback.GetProperty("samples").GetInt32() > 0
            && playback.GetProperty("channels").GetInt32() == 2
            && first.GetProperty("requestDisposed").GetBoolean() && first.GetProperty("handlerDisposed").GetBoolean()
            && first.GetProperty("clipPinned").GetBoolean();
    }

    private static bool ReadActualAoPcmGuardFailure(string path)
    {
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement owner = evidence.RootElement.GetProperty("result").GetProperty("radioSoundInitialization").GetProperty("owner");
        JsonElement item = owner.GetProperty("loadDiagnostics").GetProperty("events")[0];
        JsonElement source = item.GetProperty("source"), playback = item.GetProperty("playback");
        return owner.GetProperty("unityObjectAlive").GetBoolean()
            && source.GetProperty("pointer").GetString() == "2647DF2B4E0"
            && source.GetProperty("samples").GetInt32() == 101025 && source.GetProperty("channels").GetInt32() == 2
            && source.GetProperty("frequency").GetInt32() == 44100 && source.GetProperty("getDataSucceeded").GetBoolean()
            && playback.GetProperty("setDataSucceeded").GetBoolean() && playback.GetProperty("pointer").ValueKind == JsonValueKind.Null
            && item.GetProperty("handlerDisposed").GetBoolean() && item.GetProperty("requestDisposed").GetBoolean()
            && item.GetProperty("failure").GetString()!.Contains("Independent PCM AudioClip failed native pointer, PCM dimensions, duration", StringComparison.Ordinal);
    }

    private static bool ReadActualAmSamePointerZero(string path)
    {
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = evidence.RootElement.GetProperty("result");
        JsonElement radio = root.GetProperty("radioSoundInitialization");
        JsonElement owner = radio.GetProperty("owner");
        JsonElement events = owner.GetProperty("loadDiagnostics").GetProperty("events");
        if (radio.GetProperty("task").GetProperty("status").GetString() != "RanToCompletion"
            || !owner.GetProperty("unityObjectAlive").GetBoolean() || !owner.GetProperty("gameObjectAlive").GetBoolean()) return false;
        string[] fields = { "radioClip", "locationClip", "radioBeepClip" };
        string[] names = { "radiochat.ogg", "locationping.ogg", "radiobeep.ogg" };
        for (int i = 0; i < fields.Length; i++)
        {
            JsonElement clip = radio.GetProperty(fields[i]);
            JsonElement load = events.EnumerateArray().FirstOrDefault(x => x.GetProperty("fileName").GetString() == names[i]);
            if (load.ValueKind != JsonValueKind.Object) return false;
            JsonElement playback = load.GetProperty("playback");
            JsonElement after = load.GetProperty("afterHandlerAndRequestDispose");
            string pointer = clip.GetProperty("pointer").GetString()!;
            if (!clip.GetProperty("unityObjectAlive").GetBoolean() || clip.GetProperty("loadState").GetString() != "Loaded"
                || clip.GetProperty("length").GetSingle() != 0 || clip.GetProperty("samples").GetInt32() != 0
                || clip.GetProperty("channels").GetInt32() != 0 || clip.GetProperty("frequency").GetInt32() != 0
                || playback.GetProperty("pointer").GetString() != pointer || load.GetProperty("playbackPointerAfterRequestDispose").GetString() != pointer
                || playback.GetProperty("isDownloadedSource").GetBoolean() != true
                || !load.GetProperty("requestDisposed").GetBoolean() || load.GetProperty("handlerDisposed").GetBoolean()
                || !load.GetProperty("clipPinned").GetBoolean() || after.GetProperty("length").GetSingle() <= 0
                || after.GetProperty("samples").GetInt32() <= 0 || after.GetProperty("channels").GetInt32() <= 0
                || after.GetProperty("frequency").GetInt32() <= 0 || after.GetProperty("readError").ValueKind != JsonValueKind.Null) return false;
        }
        return true;
    }
    private static bool ReadPlaybackInterop(string interopRoot, JsonElement expected)
    {
        using var game = ModuleDefinition.ReadModule(Path.Combine(interopRoot, "Assembly-CSharp.dll"));
        using var audio = ModuleDefinition.ReadModule(Path.Combine(interopRoot, "UnityEngine.AudioModule.dll"));
        bool betterAudioReturnsSource = false;
        foreach (string candidatePath in Directory.EnumerateFiles(interopRoot, "*.dll"))
        {
            using var candidate = ModuleDefinition.ReadModule(candidatePath);
            var betterAudio = candidate.Types.SingleOrDefault(t => t.Name == "BetterAudio");
            var playAtPoint = betterAudio?.Methods.SingleOrDefault(m => m.Name == expected.GetProperty("playAtPointMethod").GetString() && m.Parameters.Count == 6);
            betterAudioReturnsSource |= playAtPoint != null && playAtPoint.ReturnType.Name == expected.GetProperty("betterSourceType").GetString() && playAtPoint.Parameters.Any(p => p.ParameterType.FullName == "UnityEngine.AudioClip");
        }
        var source = game.Types.SingleOrDefault(t => t.Name == "BetterSource");
        var gui = game.Types.SingleOrDefault(t => t.FullName == "EFT.UI.GUISounds");
        var sourceType = audio.Types.SingleOrDefault(t => t.FullName == "UnityEngine.AudioSource");
        bool sourceProps = source != null
            && source.Properties.Any(p => p.Name == expected.GetProperty("playBackStateProperty").GetString() && p.GetMethod != null)
            && source.Properties.Any(p => p.Name == expected.GetProperty("activeProperty").GetString() && p.GetMethod != null)
            && source.Properties.Any(p => p.Name == expected.GetProperty("audioSourceProperty").GetString() && p.GetMethod != null);
        bool guiVoid = gui?.Methods.Any(m => m.Name == "PlaySound" && m.Parameters.Count == 4 && m.ReturnType.FullName == "System.Void"
            && m.Parameters.Any(p => p.ParameterType.FullName == "UnityEngine.AudioClip")) == true;
        bool isPlaying = sourceType?.Properties.Any(p => p.Name == "isPlaying" && p.GetMethod?.ReturnType.FullName == "System.Boolean") == true;
        return betterAudioReturnsSource && sourceProps && guiVoid && isPlaying;
    }

    private static bool ReadHandlerInterop(string interopRoot)
    {
        using var audioRequest = ModuleDefinition.ReadModule(Path.Combine(interopRoot, "UnityEngine.UnityWebRequestAudioModule.dll"));
        using var webRequest = ModuleDefinition.ReadModule(Path.Combine(interopRoot, "UnityEngine.UnityWebRequestModule.dll"));
        using var audio = ModuleDefinition.ReadModule(Path.Combine(interopRoot, "UnityEngine.AudioModule.dll"));
        var handlerType = audioRequest.Types.SingleOrDefault(t => t.FullName == "UnityEngine.Networking.DownloadHandlerAudioClip");
        var getContent = handlerType?.Methods.SingleOrDefault(m => m.Name == "GetContent" && m.Parameters.Count == 1);
        var requestType = webRequest.Types.SingleOrDefault(t => t.FullName == "UnityEngine.Networking.UnityWebRequest");
        var handlerBase = webRequest.Types.SingleOrDefault(t => t.FullName == "UnityEngine.Networking.DownloadHandler");
        var disposeProperty = requestType?.Properties.SingleOrDefault(p => p.Name == "disposeDownloadHandlerOnDispose");
        var clipType = audio.Types.SingleOrDefault(t => t.FullName == "UnityEngine.AudioClip");
        var nativeFactory = clipType?.Methods.SingleOrDefault(m => m.Name == "Create" && m.IsStatic && m.Parameters.Count == 5);
        var setData = clipType?.Methods.SingleOrDefault(m => m.Name == "SetData" && !m.IsStatic && m.Parameters.Count == 2);
        var getData = clipType?.Methods.SingleOrDefault(m => m.Name == "GetData" && !m.IsStatic && m.Parameters.Count == 2);
        string pcmArrayType = "Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray" + (char)96 + "1<System.Single>";
        bool nativeFactoryExists = nativeFactory != null && nativeFactory.ReturnType.FullName == "UnityEngine.AudioClip"
            && nativeFactory.Parameters[0].ParameterType.FullName == "System.String"
            && nativeFactory.Parameters[1].ParameterType.FullName == "System.Int32"
            && nativeFactory.Parameters[2].ParameterType.FullName == "System.Int32"
            && nativeFactory.Parameters[3].ParameterType.FullName == "System.Int32"
            && nativeFactory.Parameters[4].ParameterType.FullName == "System.Boolean";
        bool nativePcmTransferExists = setData != null && getData != null
            && setData.ReturnType.FullName == "System.Boolean" && getData.ReturnType.FullName == "System.Boolean"
            && setData.Parameters[0].ParameterType.FullName == pcmArrayType
            && getData.Parameters[0].ParameterType.FullName == pcmArrayType
            && setData.Parameters[1].ParameterType.FullName == "System.Int32"
            && getData.Parameters[1].ParameterType.FullName == "System.Int32";
        return getContent != null && getContent.IsStatic && getContent.ReturnType.FullName == "UnityEngine.AudioClip"
            && getContent.Parameters[0].ParameterType.FullName == "UnityEngine.Networking.UnityWebRequest"
            && requestType != null && disposeProperty?.PropertyType.FullName == "System.Boolean"
            && disposeProperty.SetMethod != null && handlerBase?.Methods.Any(m => m.Name == "Dispose" && !m.IsStatic) == true
            && nativeFactoryExists && nativePcmTransferExists;
    }
    private static bool ReadActualAgCastFailure(string path, string expectedMessage, bool expectedOwnerAlive)
    {
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement radio = evidence.RootElement.GetProperty("result").GetProperty("radioSoundInitialization");
        JsonElement owner = radio.GetProperty("owner");
        JsonElement diagnostics = owner.GetProperty("loadDiagnostics").GetProperty("events");
        if (diagnostics.GetArrayLength() == 0) return false;
        JsonElement first = diagnostics[0];
        JsonElement source = first.GetProperty("source");
        return radio.GetProperty("task").GetProperty("status").GetString() == "Faulted"
            && owner.GetProperty("unityObjectAlive").GetBoolean() == expectedOwnerAlive
            && owner.GetProperty("gameObjectAlive").GetBoolean() == expectedOwnerAlive
            && first.GetProperty("failure").GetString()!.Contains(expectedMessage, StringComparison.Ordinal)
            && first.GetProperty("disposeDownloadHandlerOnDispose").ValueKind == JsonValueKind.Null
            && first.GetProperty("streamAudio").ValueKind == JsonValueKind.Null
            && first.GetProperty("compressed").ValueKind == JsonValueKind.Null
            && first.GetProperty("handlerIsDone").ValueKind == JsonValueKind.Null
            && first.GetProperty("handlerDataBytes").ValueKind == JsonValueKind.Null
            && source.GetProperty("length").ValueKind == JsonValueKind.Null
            && source.GetProperty("samples").ValueKind == JsonValueKind.Null
            && source.GetProperty("channels").ValueKind == JsonValueKind.Null
            && source.GetProperty("frequency").ValueKind == JsonValueKind.Null;
    }

    private static ReceiptClip ReadActualClip(string path)
    {
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement clip = evidence.RootElement.GetProperty("result").GetProperty("radioSoundInitialization").GetProperty("radioClip");
        return new ReceiptClip(
            clip.GetProperty("unityObjectAlive").GetBoolean(),
            clip.GetProperty("loadState").GetString()!,
            clip.GetProperty("isReadyToPlay").GetBoolean(),
            clip.GetProperty("length").GetSingle());
    }

    private static bool StrictReady(bool alive, string loadState, bool unityReady, float length, int samples, int channels, int frequency, string loadType)
        => alive && loadState == "Loaded" && unityReady && length > 0 && samples > 0 && channels > 0 && frequency > 0 && loadType != "Streaming";
}
