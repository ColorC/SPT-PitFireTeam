extern alias NativeJson;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using Comfort.Common;
using JsonSerializationException = Newtonsoft.Json.JsonSerializationException;
using EFT.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace pitTeam.Compat
{
    // Opt-in game-native test endpoint. Runs on Unity's thread, independent of OS desktop input.
    internal static class Spt5RuntimeProbe
    {
        private static readonly string? Root = Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT");
        private static string? _lastId;
        private static bool _readyWritten;
        private static FileStream? _lease;
        private static bool _leaseAttempted;
        private static readonly DateTimeOffset ActualProcessStartUtc = new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime());
        private static float _nextRead;
        private static float _nextMemorySample;
        private static bool _memorySampleFailureLogged;
        private static readonly bool RuntimeMemorySamplingEnabled = IsRuntimeMemorySamplingEnabled();
        private static readonly bool LegacyPeriodicMemorySampling = string.Equals(
            Environment.GetEnvironmentVariable("PIT_SPT5_CAPTURE_LEGACY_MEMORY"), "true", StringComparison.OrdinalIgnoreCase);
        private static Task<bool>? _bridgeCheck;
        private static string? _bridgeId;
        private static string? _shotId;
        private static string? _shotPath;
        private static float _shotDeadline;
        private static Task<object>? _spawnTask;
        private static string? _spawnId;
        private static string _spawnOperation = "spawn-follower";

                private static bool IsRuntimeMemorySamplingEnabled()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_CAPTURE_RUNTIME_MEMORY"), "true", StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_SHADOW_MODE"), "true", StringComparison.OrdinalIgnoreCase)) return false;
            if (!Uri.TryCreate(Environment.GetEnvironmentVariable("PIT_SPT5_BACKEND_URL"), UriKind.Absolute, out var backend)) return false;
            return backend.Scheme == Uri.UriSchemeHttps && backend.IsLoopback && backend.Port == 6971;
        }

        private static void WriteRuntimeMemorySample()
        {
            try
            {
                long? nativeGcUsed = null;
                long? nativeGcHeap = null;
                string? nativeGcError = null;
                try { nativeGcUsed = IL2CPP.il2cpp_gc_get_used_size(); }
                catch (Exception error) { nativeGcError = "used-size: " + error.GetType().Name + ": " + error.Message; }
                try { nativeGcHeap = IL2CPP.il2cpp_gc_get_heap_size(); }
                catch (Exception error) { nativeGcError = string.IsNullOrEmpty(nativeGcError) ? "heap-size: " + error.GetType().Name + ": " + error.Message : nativeGcError + "; heap-size: " + error.GetType().Name + ": " + error.Message; }
                bool? nativeGcDisabled = null;
                string? nativeGcDisabledError = null;
                try { nativeGcDisabled = IL2CPP.il2cpp_gc_is_disabled(); }
                catch (Exception error) { nativeGcDisabledError = error.GetType().Name + ": " + error.Message; }
                string? unityGcMode = null;
                string? unityGcModeError = null;
                try { unityGcMode = UnityEngine.Scripting.GarbageCollector.GCMode.ToString(); }
                catch (Exception error) { unityGcModeError = error.GetType().Name + ": " + error.Message; }
                bool? unityGcIncremental = null;
                string? unityGcIncrementalError = null;
                try { unityGcIncremental = UnityEngine.Scripting.GarbageCollector.isIncremental; }
                catch (Exception error) { unityGcIncrementalError = error.GetType().Name + ": " + error.Message; }
                object? protection = null;
                string? protectionError = null;
                try { protection = Spt5RuntimeWorldProbe.CaptureQaPlayerProtection(); }
                catch (Exception error) { protectionError = error.GetType().Name + ": " + error.Message; }
                var gcInfo = GC.GetGCMemoryInfo();
                using Process process = Process.GetCurrentProcess();
                var sample = new
                {
                    timestampUtc = DateTimeOffset.UtcNow,
                    processStartUtc = ActualProcessStartUtc,
                    uptimeSeconds = Math.Max(0d, (DateTimeOffset.UtcNow - ActualProcessStartUtc).TotalSeconds),
                    frame = Time.frameCount,
                    privateMemoryBytes = process.PrivateMemorySize64,
                    workingSetBytes = process.WorkingSet64,
                    virtualMemoryBytes = process.VirtualMemorySize64,
                    managedHeapBytes = GC.GetTotalMemory(false),
                    managedAllocatedBytes = GC.GetTotalAllocatedBytes(false),
                    gcHeapSizeBytes = gcInfo.HeapSizeBytes,
                    gcFragmentedBytes = gcInfo.FragmentedBytes,
                    gcCommittedBytes = gcInfo.TotalCommittedBytes,
                    gcMemoryLoadBytes = gcInfo.MemoryLoadBytes,
                    gcHighMemoryLoadThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes,
                    finalizationPendingCount = gcInfo.FinalizationPendingCount,
                    gen0Collections = GC.CollectionCount(0),
                    gen1Collections = GC.CollectionCount(1),
                    gen2Collections = GC.CollectionCount(2),
                    il2cppGcUsedBytes = nativeGcUsed,
                    il2cppGcHeapBytes = nativeGcHeap,
                    il2cppGcDisabled = nativeGcDisabled,
                    il2cppGcDisabledError = nativeGcDisabledError,
                    unityGcMode = unityGcMode,
                    unityGcModeError = unityGcModeError,
                    unityGcIsIncremental = unityGcIncremental,
                    unityGcIsIncrementalError = unityGcIncrementalError,
                    nativeGcCompatibility = Spt5NativeGcCompatibility.Snapshot(),
                    il2cppGcError = nativeGcError,
                    qaPlayerProtection = protection,
                    qaPlayerProtectionError = protectionError
                };
                string path = Path.Combine(Root!, "runtime-memory.jsonl");
                File.AppendAllText(path, JsonSerializer.Serialize(sample) + Environment.NewLine, new System.Text.UTF8Encoding(false));
            }
            catch (Exception error)
            {
                if (!_memorySampleFailureLogged)
                {
                    _memorySampleFailureLogged = true;
                    UnityEngine.Debug.LogError("[PIT SPT5 probe] Runtime memory sampling failed: " + error);
                }
            }
        }

public static void Tick(bool initialized)
        {
            if (string.IsNullOrWhiteSpace(Root) || !Directory.Exists(Root)) return;
            if (!_leaseAttempted)
            {
                _leaseAttempted = true;
                try
                {
                    _lease = File.Open(Path.Combine(Root, "probe-owner.lock"), FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.Read);
                    var owner = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        pid = Environment.ProcessId,
                        assembly = typeof(Spt5RuntimeProbe).Assembly.GetName().Name,
                        started = DateTimeOffset.UtcNow
                    }));
                    _lease.SetLength(0);
                    _lease.Write(owner);
                    _lease.Flush();
                }
                catch (IOException) { return; }
            }
            if (_lease == null) return;
            if (RuntimeMemorySamplingEnabled && initialized) Spt5FrameTimingProbe.Tick();
            // Keep the startup schema witness. During frame-pacing experiments, the
            // per-frame buffered sampler replaces periodic main-thread JSON/disk I/O.
            if (RuntimeMemorySamplingEnabled && (LegacyPeriodicMemorySampling || _nextMemorySample == 0f)
                && Time.realtimeSinceStartup >= _nextMemorySample)
            {
                _nextMemorySample = Time.realtimeSinceStartup + 10f;
                WriteRuntimeMemorySample();
            }
            if (!_readyWritten)
            {
                Reply("ready", "ready", new { pid = Environment.ProcessId, process = Environment.ProcessPath, runtime = Environment.Version.ToString(), ownerAssembly = typeof(Spt5RuntimeProbe).Assembly.GetName().Name, initialized, pitBusinessMode = Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_MODE") ?? "normal", pitBusinessState = Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE") ?? "not-reported", aiDataAssociationMode = string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "initialized", StringComparison.Ordinal) ? "post-construction" : "not-applied", pitBusinessInitialized = string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "initialized", StringComparison.Ordinal), pitBusinessSkipped = string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "skipped-observe-core", StringComparison.Ordinal) || string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "skipped-observe-ctor", StringComparison.Ordinal) });
                _readyWritten = true;
            }
            if (_bridgeCheck?.IsCompleted == true)
            {
                var check = _bridgeCheck;
                _bridgeCheck = null;
                try { Reply(_bridgeId!, "bridge", new { passed = check.GetAwaiter().GetResult() }); }
                catch (Exception error) { Reply(_bridgeId!, "bridge", new { passed = false, error = error.ToString() }); }
            }
            if (_spawnTask != null && _spawnTask.IsCompleted) FinishSpawn();
            if (_shotId != null && (File.Exists(_shotPath) || Time.realtimeSinceStartup > _shotDeadline))
            {
                Reply(_shotId, "screenshot", new { path = _shotPath, exists = File.Exists(_shotPath) });
                _shotId = null;
            }
            if (Time.realtimeSinceStartup < _nextRead) return;
            _nextRead = Time.realtimeSinceStartup + 0.5f;
            var request = Path.Combine(Root, "probe-command.json");
            if (!File.Exists(request)) return;
            string? id = null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(request));
                var command = doc.RootElement;
                int targetPid = command.GetProperty("targetPid").GetInt32();
                DateTimeOffset issuedAt = DateTimeOffset.Parse(command.GetProperty("issuedAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (targetPid != Environment.ProcessId || issuedAt < ActualProcessStartUtc) return;
                id = command.GetProperty("id").GetString()!;
                if (id == _lastId) return;
                _lastId = id;
                var operation = command.GetProperty("operation").GetString();
                switch (operation)
                {
                    case "ready":
                        Reply(id, operation, new { ready = true, actualStartUtc = ActualProcessStartUtc });
                        break;
                    case "inspect":
                        Reply(id, operation, Spt5RuntimeUiProbe.Inspect(initialized));
                        break;
case "hover":
                        string hoverPath = command.GetProperty("path").GetString()!;
                        string hoverType = command.GetProperty("targetType").GetString()!;
                        int hoverInstanceId = command.TryGetProperty("instanceId", out var hoverTargetId) ? hoverTargetId.GetInt32() : 0;
                        Reply(id, operation, Spt5RuntimeUiProbe.Hover(hoverPath, hoverType, hoverInstanceId));
                        break;
                    case "set-input-text":
                        string inputPath = command.GetProperty("path").GetString()!;
                        string inputType = command.GetProperty("targetType").GetString()!;
                        int inputInstanceId = command.GetProperty("instanceId").GetInt32();
                        string inputValue = command.GetProperty("inputText").GetString()!;
                        bool commitInput = command.TryGetProperty("commitInput", out var commitInputValue) && commitInputValue.GetBoolean();
                        Reply(id, operation, Spt5RuntimeUiProbe.SetInputText(inputPath, inputType, inputInstanceId, inputValue, commitInput));
                        break;
                    case "scroll":
                        Reply(id, operation, Spt5RuntimeUiProbe.Scroll(command.GetProperty("path").GetString()!, command.GetProperty("targetType").GetString()!, command.GetProperty("instanceId").GetInt32(), command.GetProperty("scrollDeltaY").GetSingle()));
                        break;
                    case "editor-quick-move":
                        Reply(id, operation, Spt5RuntimeUiProbe.QuickMoveEditorItem(command.GetProperty("path").GetString()!, command.GetProperty("targetType").GetString()!, command.GetProperty("instanceId").GetInt32()));
                        break;
                    case "qa-exfil-now":
                        Reply(id, operation, Spt5RuntimeWorldProbe.ExtractWithInstalledQaMod());
                        break;
                    case "click":
                        string path = command.GetProperty("path").GetString()!;
                        string targetType = command.GetProperty("targetType").GetString()!;
                        int instanceId = command.TryGetProperty("instanceId", out var targetId) ? targetId.GetInt32() : 0;
                        string mouseButton = command.TryGetProperty("mouseButton", out var buttonProperty) ? buttonProperty.GetString() ?? "left" : "left";
                        Reply(id, operation, Spt5RuntimeUiProbe.Click(path, targetType, instanceId, mouseButton));
                        break;
                    case "screenshot":
                        _shotId = id;
                        _shotPath = Path.Combine(Root, "probe-" + SafeId(id) + ".png");
                        _shotDeadline = Time.realtimeSinceStartup + 10;
                        ScreenCapture.CaptureScreenshot(_shotPath);
                        break;
                    case "json":
                        string nativeNickname = command.GetProperty("nativeNickname").GetString()!;
                        if (string.IsNullOrWhiteSpace(nativeNickname)) throw new ArgumentException("Native nickname must not be empty.");
                        var session = Singleton<ClientApplication<IEftSession>>.Instance?.GetClientBackEndSession();
                        ProfileInfo liveInfo = session?.Profile?.Info ?? throw new InvalidOperationException("No live backend Profile.Info is available for the JSON probe.");
                        var controlledDescriptor = new ProfileInfoDescriptor(liveInfo);
                        controlledDescriptor.Nickname = nativeNickname;
                        var payload = new JsonProbePayload { Number = 37, Native = liveInfo, Controlled = controlledDescriptor };
                        string encoded = SptJsonBoundary.Serialize(payload);
                        var decoded = SptJsonBoundary.Deserialize<JsonProbePayload>(encoded);
                        bool malformedRejected = false;
                        try { SptJsonBoundary.Deserialize<ProfileInfo>("{\"Experience\":37}"); }
                        catch (JsonSerializationException) { malformedRejected = true; }
                        var nativeSmoke = RunNativeJObjectSmoke(encoded);
                        bool passed = nativeSmoke.Passed && decoded.Number == 37 && decoded.Native != null && decoded.Controlled != null
                            && decoded.Native.Nickname == liveInfo.Nickname && decoded.Native.Experience == liveInfo.Experience
                            && decoded.Native.Level == liveInfo.Level && decoded.Controlled.Nickname == nativeNickname
                            && decoded.Controlled.Experience == liveInfo.Experience && malformedRejected;
                        Reply(id, operation, new { passed, nativeJObject = nativeSmoke.Evidence, number = decoded.Number, liveNickname = liveInfo.Nickname, nicknameAfterRoundtrip = decoded.Native?.Nickname, expectedControlledNickname = nativeNickname, controlledNicknameAfterRoundtrip = decoded.Controlled?.Nickname, experience = liveInfo.Experience, experienceAfterRoundtrip = decoded.Native?.Experience, level = liveInfo.Level, levelAfterRoundtrip = decoded.Native?.Level, malformedRejected, json = encoded });
                        break;
                    case "qa-face":
                        Reply(id, operation, Spt5RuntimeRaidProbe.Face(new Vector3(command.GetProperty("targetX").GetSingle(), command.GetProperty("targetY").GetSingle(), command.GetProperty("targetZ").GetSingle())));
                        break;
                    case "qa-navigate":
                        if (_spawnTask != null) throw new InvalidOperationException("asynchronous-operation-already-running");
                        _spawnId = id;
                        _spawnOperation = operation;
                        _spawnTask = Spt5RuntimeRaidProbe.Navigate(new Vector3(command.GetProperty("targetX").GetSingle(), command.GetProperty("targetY").GetSingle(), command.GetProperty("targetZ").GetSingle()));
                        break;
                    case "install-follow":
                        if (_spawnTask != null) throw new InvalidOperationException("asynchronous-operation-already-running");
                        _spawnId = id;
                        _spawnOperation = operation;
                        _spawnTask = Spt5RuntimeRaidProbe.Follow(SafeId(id));
                        break;
                    case "install-sample":
                        Spt5RuntimeRaidProbe.RequireShadow();
                        Reply(id, operation, Spt5RuntimeWorldProbe.CaptureInstallSample());
                        break;
                    case "install-status":
                        Reply(id, operation, Spt5RuntimeWorldProbe.ReportInstallStatus());
                        break;
                    case "door-crossing-smoke":
                        Spt5RuntimeRaidProbe.RequireShadow();
                        Reply(id, operation, Spt5RuntimeWorldProbe.RunDoorCrossingSmoke());
                        break;
                    case "install-command":
                        Spt5RuntimeRaidProbe.RequireShadow();
                        Reply(id, operation, Spt5RuntimeWorldProbe.RunInstallCommand(command.GetProperty("commandName").GetString()!));
                        break;
                    case "appearance-options":
                        Spt5RuntimeRaidProbe.RequireShadow();
                        Reply(id, operation, Spt5RuntimeWorldProbe.CaptureAppearanceOptions());
                        break;
                    case "install-loot-body":
                        Spt5RuntimeRaidProbe.RequireShadow();
                        Reply(id, operation, Spt5RuntimeWorldProbe.ReportInstallLootBody());
                        break;
                    case "install-protection-off":
                        Spt5RuntimeRaidProbe.RequireShadow();
                        Reply(id, operation, Spt5RuntimeWorldProbe.SetQaPlayerProtectionEnabled(false, false));
                        break;
                    case "world":
                        Reply(id, operation, Spt5RuntimeWorldProbe.Capture());
                        break;
                    case "qa-native-gc":
                    {
                        string? nativeGcMode = command.TryGetProperty("nativeGcMode", out var gcModeValue) && gcModeValue.ValueKind == JsonValueKind.String ? gcModeValue.GetString() : null;
                        Reply(id, operation, Spt5QaNativeGcControl.Apply(nativeGcMode));
                        break;
                    }
                    case "qa-player-protection":
                    {
                        bool enableQaProtection = command.GetProperty("enable").GetBoolean();
                        bool fullHealthMaintenance = !command.TryGetProperty("fullHealthMaintenance", out var maintenance) || maintenance.GetBoolean();
                        Reply(id, operation, Spt5RuntimeWorldProbe.SetQaPlayerProtectionEnabled(enableQaProtection, fullHealthMaintenance));
                        break;
                    }
                    case "being-hit-action-smoke":
                        Reply(id, operation, Spt5RuntimeWorldProbe.RunBeingHitActionBridgeSmoke());
                        break;
                    case "spawn-follower":
                        _spawnId = id;
                        _spawnOperation = operation;
                        _spawnTask = Spt5RuntimeWorldProbe.SpawnFollower();
                        break;
                    case "prewarm-targets": Reply(id, operation, InspectRuntimeTargets(false)); break;
                    case "load-targets": Reply(id, operation, InspectRuntimeTargets(true)); break;
                    case "dogtag-identity": { Reply(id, operation, CheckDogtagIdentity()); break; }
                    case "bridge":
                        _bridgeId = id;
                        _bridgeCheck = CheckBridge();
                        break;
                    default: throw new ArgumentException("Unknown runtime probe operation: " + operation);
                }
            }
            catch (Exception error) { Reply(id ?? "invalid", "error", new { error = error.ToString() }); }
        }

        private static void FinishSpawn()
        {
            var task = _spawnTask!;
            _spawnTask = null;
            try { Reply(_spawnId!, _spawnOperation, task.GetAwaiter().GetResult()); }
            catch (Exception error) { Reply(_spawnId!, _spawnOperation, new { passed = false, error = error.ToString() }); }
        }
        private static (bool Passed, object Evidence) RunNativeJObjectSmoke(string encoded)
        {
            string phase = "native-params-parse";
            Il2CppSystem.Object? nativeParams = null;
            SendRequest? request = null;
            IntPtr paramsPointer = IntPtr.Zero;
            IntPtr requestPointer = IntPtr.Zero;
            bool serializationEquivalent = false;
            bool paramsSetterSucceeded = false;
            IntPtr paramsGetterPointer = IntPtr.Zero;
            bool paramsReadbackMatches = false;
            object nativeTypeFacts;
            try
            {
                Type nativeAliasType = typeof(NativeJson::Newtonsoft.Json.Linq.JObject);
                PropertyInfo? paramsProperty = typeof(SendRequest).GetProperty("Params");
                nativeTypeFacts = new
                {
                    available = true,
                    assemblyFullName = nativeAliasType.Assembly.FullName,
                    assemblyLocation = nativeAliasType.Assembly.Location,
                    baseType = nativeAliasType.BaseType?.FullName,
                    hasExactIntPtrConstructor = nativeAliasType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(ctor =>
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(IntPtr);
                }),
                    sendRequestParamsPropertyType = paramsProperty?.PropertyType.AssemblyQualifiedName
                };
            }
            catch (Exception typeError)
            {
                nativeTypeFacts = new { available = false, error = typeError.ToString() };
            }
            try
            {
                nativeParams = SptJsonBoundary.ParseNativeJObject(encoded);
                phase = "native-params-pointer";
                paramsPointer = nativeParams.Pointer;
                if (paramsPointer == IntPtr.Zero)
                    throw new InvalidOperationException("Native Object wrapper pointer was zero.");

                phase = "native-params-serialize";
                string nativeJson = SptJsonBoundary.Serialize(nativeParams);
                serializationEquivalent = Newtonsoft.Json.Linq.JToken.DeepEquals(
                    Newtonsoft.Json.Linq.JToken.Parse(encoded),
                    Newtonsoft.Json.Linq.JToken.Parse(nativeJson));

                phase = "send-request-create";
                request = new SendRequest();
                phase = "send-request-pointer";
                requestPointer = request.Pointer;
                if (requestPointer == IntPtr.Zero)
                    throw new InvalidOperationException("Native SendRequest pointer was zero.");

                phase = "send-request-params-set";
                request.Params = nativeParams;
                paramsSetterSucceeded = true;
                phase = "send-request-params-readback";
                paramsGetterPointer = request.Params?.Pointer ?? IntPtr.Zero;
                paramsReadbackMatches = paramsGetterPointer != IntPtr.Zero && paramsGetterPointer == paramsPointer;
                if (!paramsReadbackMatches)
                    throw new InvalidOperationException("SendRequest.Params getter did not return the assigned native Params pointer.");
                return (serializationEquivalent && paramsReadbackMatches, new
                {
                    passed = serializationEquivalent && paramsReadbackMatches,
                    nativeTypeFacts,
                    phase = "completed",
                    nativeParamsPointer = paramsPointer.ToString("X"),
                    sendRequestPointer = requestPointer.ToString("X"),
                    paramsGetterPointer = paramsGetterPointer.ToString("X"),
                    paramsSetterSucceeded,
                    paramsReadbackMatches,
                    serializationEquivalent,
                    sent = false
                });
            }
            catch (Exception error)
            {
                return (false, new
                {
                    passed = false,
                    nativeTypeFacts,
                    phase,
                    nativeParamsPointer = paramsPointer.ToString("X"),
                    sendRequestPointer = requestPointer.ToString("X"),
                    paramsGetterPointer = paramsGetterPointer.ToString("X"),
                    paramsSetterSucceeded,
                    paramsReadbackMatches,
                    serializationEquivalent,
                    sent = false,
                    error = error.ToString()
                });
            }
            finally
            {
                GC.KeepAlive(request);
                GC.KeepAlive(nativeParams);
            }
        }

        private sealed class JsonProbePayload
        {
            public int Number { get; set; }
            public ProfileInfo? Native { get; set; }
            public ProfileInfoDescriptor? Controlled { get; set; }
        }
        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var node = transform; node != null; node = node.parent) parts.Add(node.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Type RequireCoreProbeType()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "pitFireTeam");
            if (assembly == null) throw new NotSupportedException("pitFireTeam core is not loaded; optional audit unsupported.");
            return assembly.GetType("pitTeam.Patches.TeammateCorpseDogtagProbe", false)
                ?? throw new NotSupportedException("TeammateCorpseDogtagProbe is unavailable in core.");
        }
        private static object InvokeCoreProbe(string name, Type[] types, object[] args)
        {
            var method = RequireCoreProbeType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null);
            if (method == null) throw new NotSupportedException("Core probe member is unavailable: " + name);
            try { return method.Invoke(null, args) ?? throw new NotSupportedException("Core probe returned null: " + name); }
            catch (TargetInvocationException error) { throw new InvalidOperationException("Core probe failed: " + name, error.InnerException ?? error); }
        }
        private static object InvokeAuditGeneratedTarget(Type type, string field, string method, string result, string[] names, string[] types, bool isStatic)
        {
            return InvokeCoreProbe("AuditGeneratedTarget", new[] { typeof(Type), typeof(string), typeof(string), typeof(string), typeof(string[]), typeof(string[]), typeof(bool) }, new object[] { type, field, method, result, names, types, isStatic });
        }
        private static object InvokeAuditNativeInstanceFields(Type type)
        {
            return InvokeCoreProbe("AuditNativeInstanceFields", new[] { typeof(Type) }, new object[] { type });
        }
        private static void SetNativeInstanceFields(object target, object audit)
        {
            var property = target.GetType().GetProperty("nativeInstanceFields", BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite) throw new NotSupportedException("nativeInstanceFields setter unavailable.");
            property.SetValue(target, audit);
        }
        private static object InspectRuntimeTargets(bool includeWeather)
        {
            var prewarmTargets = new object[]
            {
                InvokeAuditGeneratedTarget(typeof(EFT.Ballistics.Shot),
                    "NativeMethodInfoPtr_Init_Protected_Void_Item_Int32_Int32_Vector3_Vector3_Single_Single_Single_Single_Single_Single_Single_Single_Single_Single_Int32_Int32_BallisticCollider_IRandom_Single_Vector3_Int32_Item_Int32_Shot_Boolean_0",
                    "Init", "System.Void",
                    new[] { "ammo", "fragmentIndex", "seed", "origin", "direction", "initialSpeed", "speed", "bulletMassKg", "bulletDiameterM", "damage", "penetrationPower", "penetrationChance", "ricochetChance", "fragmentationChance", "deviationChance", "minFragmentsCount", "maxFragmentsCount", "defaultBallisticCollider", "randoms", "ballisticCoefficient", "abc", "playerRaidId", "weapon", "fireIndex", "parent", "delayedDamage" },
                    new[] { "EFT.InventoryLogic.Item", "System.Int32", "System.Int32", "UnityEngine.Vector3", "UnityEngine.Vector3", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Int32", "System.Int32", "EFT.Ballistics.BallisticCollider", "EFT.GameRandoms.IRandom", "System.Single", "UnityEngine.Vector3", "System.Int32", "EFT.InventoryLogic.Item", "System.Int32", "EFT.Ballistics.Shot", "System.Boolean" }, false),
                InvokeAuditGeneratedTarget(typeof(EFT.Ballistics.Shot),
                    "NativeMethodInfoPtr_Create_Public_Static_Shot_Item_Int32_Int32_Vector3_Vector3_Single_Single_Single_Single_Single_Single_Single_Single_Single_Single_Int32_Int32_BallisticCollider_IRandom_Single_Vector3_Int32_Item_Int32_Shot_Boolean_0",
                    "Create", "EFT.Ballistics.Shot",
                    new[] { "ammo", "fragmentIndex", "seed", "origin", "direction", "initialSpeed", "speed", "bulletMassGram", "bulletDiameterMilimeters", "damage", "penetrationPower", "penetrationChance", "ricochetChance", "fragmentationChance", "deviationChance", "minFragmentsCount", "maxFragmentsCount", "defaultBallisticCollider", "randoms", "ballisticCoefficient", "abc", "player", "weapon", "fireIndex", "parent", "delayedDamage" },
                    new[] { "EFT.InventoryLogic.Item", "System.Int32", "System.Int32", "UnityEngine.Vector3", "UnityEngine.Vector3", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Single", "System.Int32", "System.Int32", "EFT.Ballistics.BallisticCollider", "EFT.GameRandoms.IRandom", "System.Single", "UnityEngine.Vector3", "System.Int32", "EFT.InventoryLogic.Item", "System.Int32", "EFT.Ballistics.Shot", "System.Boolean" }, true),
                InvokeAuditGeneratedTarget(typeof(EFT.Ballistics.BallisticsCalculator),
                    "NativeMethodInfoPtr_CreateShot_Public_Shot_Ammo_Vector3_Vector3_Int32_Vector3_Int32_Item_Single_Int32_0",
                    "CreateShot", "EFT.Ballistics.Shot",
                    new[] { "ammo", "origin", "direction", "fireIndex", "abc", "playerRaidId", "weapon", "speedFactor", "fragmentIndex" },
                    new[] { "EFT.InventoryLogic.Ammo", "UnityEngine.Vector3", "UnityEngine.Vector3", "System.Int32", "UnityEngine.Vector3", "System.Int32", "EFT.InventoryLogic.Item", "System.Single", "System.Int32" }, false),
                InvokeAuditGeneratedTarget(typeof(EFT.Ballistics.BallisticCalculatorPrewarmer),
                    "NativeMethodInfoPtr_SimulateShot_Public_Shot_0",
                    "SimulateShot", "EFT.Ballistics.Shot", Array.Empty<string>(), Array.Empty<string>(), false)
            };
            object[] targets = prewarmTargets;
            if (includeWeather)
            {
                Type? weatherType = typeof(EFT.LocalGame).BaseType;
                if (weatherType == null || !weatherType.IsGenericType
                    || weatherType.GetGenericTypeDefinition().FullName != "EFT.BaseLocalGame`1"
                    || weatherType.GetGenericArguments().Length != 1
                    || weatherType.GetGenericArguments()[0].FullName != "EFT.EftGamePlayerOwner")
                {
                    return new { status = "error", scope = "closed LocalGame BaseLocalGame weather target", error = "LocalGame.BaseType did not match BaseLocalGame<EftGamePlayerOwner>.", targets = prewarmTargets };
                }
                var weatherTarget = InvokeAuditGeneratedTarget(weatherType,
                    "NativeMethodInfoPtr_InitWeather_Private_Void_TimeAndWeatherSettings_0",
                    "InitWeather", "System.Void", new[] { "timeAndWeather" },
                    new[] { "EFT.TimeAndWeatherSettings" }, false);
                SetNativeInstanceFields(weatherTarget, InvokeAuditNativeInstanceFields(weatherType));
                targets = prewarmTargets.Concat(new object[] { weatherTarget }).ToArray();
            }
            var statuses = targets.Select(target =>
                (string?)target.GetType().GetProperty("status")?.GetValue(target)).ToArray();
            var fieldAudits = targets.Select(target => target.GetType().GetProperty("nativeInstanceFields")?.GetValue(target))
                .Where(audit => audit != null).ToArray();
            var fieldAuditStatuses = fieldAudits.Select(audit =>
                (string?)audit!.GetType().GetProperty("status")?.GetValue(audit)).ToArray();
            bool passed = statuses.Length == targets.Length && statuses.All(status => status == "ok")
                && fieldAuditStatuses.All(status => status == "ok");
            return new { status = passed ? "ok" : "partial", scope = includeWeather ? "four fixed Ballistics prewarm targets plus closed LocalGame weather init; aliases limited to declaring classes" : "four fixed Ballistics prewarm wrapper targets; aliases limited to each declaring class", passed, targetCount = targets.Length, fieldAuditStatuses, targets };
        }

        private static object CheckDogtagIdentity()
        {
            var session = Singleton<ClientApplication<IEftSession>>.Instance?.GetClientBackEndSession();
            var equipment = session?.Profile?.Inventory?.Equipment;
            if (equipment == null) return new { passed = false, reason = "No live equipment." };
            var items = new List<EFT.InventoryLogic.Item>();
            foreach (EFT.InventoryLogic.EquipmentSlot slotType in Enum.GetValues(typeof(EFT.InventoryLogic.EquipmentSlot))) { var live = equipment.GetSlot(slotType)?.ContainedItem; if (live != null && live.Pointer != IntPtr.Zero && !items.Any(x => x.Pointer == live.Pointer)) items.Add(live); }
            if (items.Count < 2) return new { passed = false, reason = "Need two live items." };
            var first = items[0]; var other = items[1]; var wrapper = new EFT.InventoryLogic.Item(first.Pointer);
            bool refDistinct = !ReferenceEquals(first, wrapper); bool same = Il2CppBoundary.SameNativeObject(first, wrapper); bool otherFalse = !Il2CppBoundary.SameNativeObject(first, other); bool nullFalse = !Il2CppBoundary.SameNativeObject(null, first) && !Il2CppBoundary.SameNativeObject(null, null);
            return new { passed = refDistinct && same && otherFalse && nullFalse, refDistinct, same, otherFalse, nullFalse };
        }
        private static string SafeId(string id)
        {
            if (id.Length == 0 || id.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
                throw new ArgumentException("Probe IDs accept letters, numbers and hyphens.");
            return id;
        }

        private static void Reply(string id, string operation, object result)
        {
            string target = Path.Combine(Root!, "probe-" + SafeId(id) + ".json");
            string staging = target + ".writing";
            File.WriteAllText(staging, JsonSerializer.Serialize(new { id, operation, pid = Environment.ProcessId,
                ownerAssembly = typeof(Spt5RuntimeProbe).Assembly.GetName().Name, process = Environment.ProcessPath,
                actualStartUtc = ActualProcessStartUtc, timestamp = DateTimeOffset.UtcNow, result }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(staging, target, true);
        }

        private static async Task<bool> CheckBridge()
        {
            var success = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<int>();
            var fault = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<int>();
            var cancel = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<int>();
            var a = Il2CppTaskBridge.FromNative(success.Task);
            var b = Il2CppTaskBridge.FromNative(fault.Task);
            var c = Il2CppTaskBridge.FromNative(cancel.Task);
            success.SetResult(42);
            fault.TrySetException(new Il2CppSystem.Exception("native fixture fault"));
            cancel.TrySetCanceled();
            bool passed = await a == 42;
            try { await b; passed = false; } catch (InvalidOperationException) { }
            try { await c; passed = false; } catch (TaskCanceledException) { }
            var x = Il2CppTaskBridge.ToNative(Task.FromResult(7));
            var y = Il2CppTaskBridge.ToNative(Task.FromException<int>(new Exception("managed fixture fault")));
            var z = Il2CppTaskBridge.ToNative(Task.FromCanceled<int>(new System.Threading.CancellationToken(true)));
            passed &= await Il2CppTaskBridge.FromNative(x) == 7;
            try { await Il2CppTaskBridge.FromNative(y); passed = false; } catch (InvalidOperationException) { }
            try { await Il2CppTaskBridge.FromNative(z); passed = false; } catch (TaskCanceledException) { }
            var emptyProfiles = new Il2CppReferenceArray<Profile>(0);
            var nativeLoadBots = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<Il2CppReferenceArray<Profile>>();
            var managedLoadBots = Il2CppTaskBridge.FromNative(nativeLoadBots.Task);
            nativeLoadBots.SetResult(emptyProfiles);
            var loadedProfiles = await managedLoadBots;
            passed &= loadedProfiles.Length == 0;
            var managedLoadBotsResult = Task.FromResult(emptyProfiles);
            var nativeLoadBotsResult = Il2CppTaskBridge.ToNative(managedLoadBotsResult);
            var roundTrippedProfiles = await Il2CppTaskBridge.FromNative(nativeLoadBotsResult);
            passed &= roundTrippedProfiles.Length == 0;
            return passed;
        }
    }
}
