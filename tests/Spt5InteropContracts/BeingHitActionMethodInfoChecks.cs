using Mono.Cecil;
using System.Text.Json;
internal static partial class Program
{
    static int BeingHitActionMethodInfoSelfTest(Args a)
    {
        string bridgePath = File(a.R("bridge"));
        string runtimePath = File(a.R("runtime"));
        string interopPath = File(a.R("interop"));
        string source = System.IO.File.ReadAllText(bridgePath);
        var checks = new List<MethodInfoCheck>();
        void Check(string name, bool passed, string evidence) => checks.Add(new MethodInfoCheck(name, passed, evidence));
        bool typedLookup = source.Contains("IL2CPP.il2cpp_class_get_type(actionClass)", StringComparison.Ordinal)
            && source.Contains("Il2CppSystem.Type.internal_from_handle(nativeTypeHandle)", StringComparison.Ordinal)
            && source.Contains("actionNativeType.GetMethod(\"Invoke\")", StringComparison.Ordinal)
            && source.Contains("NativeActionInvokeMethod(actionClass)", StringComparison.Ordinal);
        Check("normal-factory-uses-typed-reflection-method", typedLookup,
            "CreateSubscription resolves Action3.Invoke through class_get_type -> internal_from_handle -> GetMethod(Invoke).");
        bool noRawMisconstruction = !source.Contains("new Il2CppSystem.Reflection.MethodInfo(", StringComparison.Ordinal)
            && !source.Contains("NativeInvokeInfoPointer", StringComparison.Ordinal);
        Check("raw-method-info-pointer-not-wrapped-as-reflection-object", noRawMisconstruction,
            "Generated NativeMethodInfoPtr_Invoke is an Il2CppMethodInfo pointer; the Reflection.MethodInfo(IntPtr) constructor expects a reflection object pointer.");
        bool bodyPartActual = source.Contains("typeof(EBodyPart).FullName", StringComparison.Ordinal)
            && !source.Contains("ParameterType.FullName != \"EBodyPart\"", StringComparison.Ordinal);
        Check("invoke-validator-uses-actual-bodypart-fullname", bodyPartActual, "The validator uses typeof(EBodyPart).FullName.");
        using var runtime = AssemblyDefinition.ReadAssembly(runtimePath);
        using var interop = AssemblyDefinition.ReadAssembly(interopPath);
        var handler = runtime.MainModule.GetType("Il2CppInterop.Runtime.Runtime.UnityVersionHandler");
        var nativeInfo = runtime.MainModule.GetType("Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo.INativeMethodInfoStruct");
        var nativeType = interop.MainModule.GetType("Il2CppSystem.Type");
        var reflectionMethod = interop.MainModule.GetType("Il2CppSystem.Reflection.MethodInfo");
        bool newMethod = handler?.Methods.Any(m => m.Name == "NewMethod" && m.Parameters.Count == 0) == true;
        bool setter = nativeInfo?.Methods.Any(m => m.Name == "set_IsMarshalledFromNative" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Boolean") == true;
        bool typedRefMethod = nativeInfo?.Properties.Any(p => p.Name == "MethodPointer" && p.PropertyType.FullName == "System.IntPtr&") == true
            && nativeInfo?.Properties.Any(p => p.Name == "ParametersCount" && p.PropertyType.FullName == "System.Byte&") == true
            && nativeInfo?.Properties.Any(p => p.Name == "Slot" && p.PropertyType.FullName == "System.UInt16&") == true;
        Check("loaded-runtime-native-method-info-shape", newMethod && setter && typedRefMethod,
            "NewMethod exists; IsMarshalledFromNative has a Boolean setter; MethodPointer is IntPtr&, ParametersCount Byte&, Slot UInt16&.");
        bool typedReflectionLookup = nativeType?.Methods.Any(m => m.Name == "internal_from_handle" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.IntPtr" && m.ReturnType.FullName == "Il2CppSystem.Type") == true
            && nativeType?.Methods.Any(m => m.Name == "GetMethod" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String" && m.ReturnType.FullName == "Il2CppSystem.Reflection.MethodInfo") == true;
        bool rawCtorDistinguished = reflectionMethod?.Methods.Any(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.IntPtr") == true;
        Check("loaded-interop-provides-typed-type-to-method-path", typedReflectionLookup && rawCtorDistinguished,
            "Type.internal_from_handle(IntPtr) returns Il2CppSystem.Type; GetMethod(String) returns Il2CppSystem.Reflection.MethodInfo; the raw wrapper ctor(IntPtr) exists but is not used.");
        string gameAssemblyPath = Path.Combine(Path.GetDirectoryName(interopPath)!, "Assembly-CSharp.dll");
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bridgePath)!, "..", ".."));
        string normalConsumerPath = Path.Combine(repoRoot, "client", "Patches", "ExUsecBrainPatch.cs");
        using var game = AssemblyDefinition.ReadAssembly(gameAssemblyPath);
        var playerBridge = game.MainModule.GetType("EFT.PlayerBridge");
        var observerContract = game.MainModule.GetType("EFT.IObserverToPlayerBridge");
        var playerType = game.MainModule.GetType("EFT.Player");
        var localPlayerType = game.MainModule.GetType("EFT.LocalPlayer");
        var damageInfo = game.MainModule.GetType("EFT.Ballistics.DamageInfo");
        bool playerBridgeContract = observerContract?.IsInterface == true && playerBridge != null && playerBridge.Interfaces.Any(i => i.InterfaceType.FullName == "EFT.IObserverToPlayerBridge")
            && playerBridge.Methods.Any(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "EFT.Player");
        var playerCtor = playerBridge?.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "EFT.Player");
        bool nativeBridgeConstruction = playerCtor != null
            && playerCtor.Body.Instructions.Any(i => i.Operand is MethodReference mr && mr.Name == "il2cpp_object_new")
            && playerCtor.Body.Instructions.Any(i => i.Operand is MethodReference mr && mr.Name == "Il2CppObjectBaseToPtr")
            && playerCtor.Body.Instructions.Any(i => i.Operand is MethodReference mr && mr.Name == ".ctor" && mr.Parameters.Count == 1 && mr.Parameters[0].ParameterType.FullName == "System.IntPtr");
        bool damageInfoPlayerContract = damageInfo != null && damageInfo.Methods.Any(m => m.Name == "get_Player" && m.ReturnType.FullName == "EFT.IObserverToPlayerBridge")
            && damageInfo.Methods.Any(m => m.Name == "set_Player" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "EFT.IObserverToPlayerBridge");
        bool directPlayerCastIsInvalid = playerType != null && localPlayerType != null && !playerType.Interfaces.Any(i => i.InterfaceType.FullName == "EFT.IObserverToPlayerBridge")
            && localPlayerType?.BaseType?.FullName == "EFT.Player";
        Check("loaded-game-playerbridge-is-the-observer-adapter", playerBridgeContract && nativeBridgeConstruction && damageInfoPlayerContract && directPlayerCastIsInvalid,
            "Actual Assembly-CSharp has PlayerBridge : IObserverToPlayerBridge with Player and IntPtr constructors; Player itself does not implement the observer contract, LocalPlayer derives from Player; DamageInfo.Player getter/setter use the observer interface. PlayerBridge(Player) allocates its own native bridge object and passes the Player pointer to its native constructor.");
        string consumer = System.IO.File.ReadAllText(normalConsumerPath);
        bool normalConsumerUsesObserverPlayer = consumer.Contains("damageinfo.Player", StringComparison.Ordinal) && consumer.Contains("player.iPlayer", StringComparison.Ordinal);
        Check("normal-damageinfo-player-consumer-uses-bridge-iPlayer", normalConsumerUsesObserverPlayer,
            Path.GetFullPath(normalConsumerPath) + " reads DamageInfo.Player then accesses its iPlayer bridge member.");
        bool sampleUsesNativeBridgeObject = source.Contains("new EFT.PlayerBridge(samplePlayer)", StringComparison.Ordinal)
            && source.Contains("sample.Player = observerBridge", StringComparison.Ordinal)
            && source.Contains("expectedBridgePointer = observerBridge.Pointer", StringComparison.Ordinal)
            && source.Contains("expectedPlayerPointer = samplePlayer.Pointer", StringComparison.Ordinal)
            && source.Contains("actualBridgePointer == expectedBridgePointer && actualPlayerPointer == expectedPlayerPointer", StringComparison.Ordinal)
            && source.Contains("observer.iPlayer", StringComparison.Ordinal)
            && source.Contains("GC.KeepAlive(observerBridge)", StringComparison.Ordinal)
            && !source.Contains("samplePlayer.Cast<EFT.IObserverToPlayerBridge>()", StringComparison.Ordinal);
        Check("actual-player-smoke-uses-playerbridge-pointer", sampleUsesNativeBridgeObject,
            "The sample setter uses EFT.PlayerBridge; the smoke compares info.Player.Pointer with the adapter pointer and info.Player.iPlayer.Pointer separately with the original Player pointer.");
        int smokeStart = source.IndexOf("RunControlledNativeSmoke(Player? samplePlayer)", StringComparison.Ordinal);
        int directStart = source.IndexOf("RunControlledNativeSmoke(EFT.Ballistics.DamageInfo sample", StringComparison.Ordinal);
        int readPointerStart = source.IndexOf("private static IntPtr ReadNativeObjectPointer", directStart, StringComparison.Ordinal);
        string smokeMethods = smokeStart >= 0 && directStart > smokeStart && readPointerStart > directStart ? source.Substring(smokeStart, readPointerStart - smokeStart) : string.Empty;
        bool smokeErrorsIsolated = smokeMethods.Contains("RecordSmokeError(error)", StringComparison.Ordinal)
            && smokeMethods.Contains("smokeErrors = smokeErrorCount", StringComparison.Ordinal)
            && smokeMethods.Contains("firstSmokeError = smokeErrorCount == 0 ? null : Volatile.Read(ref _firstSmokeError)", StringComparison.Ordinal)
            && smokeMethods.Contains("available = playerReferenceReady", StringComparison.Ordinal)
            && !smokeMethods.Contains("RecordError(error)", StringComparison.Ordinal)
            && source.Contains("if (_controlledSmoke) Spt5BeingHitActionBridge.RecordSmokeError(error); else Spt5BeingHitActionBridge.RecordError(error);", StringComparison.Ordinal)
            && source.Contains("smokeErrors = Volatile.Read(ref _smokeErrors)", StringComparison.Ordinal)
            && source.Contains("status = Volatile.Read(ref _errors) == 0", StringComparison.Ordinal)
            && !smokeMethods.Contains("Interlocked.Increment(ref _attempts)", StringComparison.Ordinal);
        Check("controlled-smoke-failure-does-not-pollute-normal-subscription-state", smokeErrorsIsolated,
            "Test-only activation/callback failures go to smokeErrors/firstSmokeError and smokeAttempts; passive normal errors/status/attempts remain the real BeingHitAction subscription counters.");
        int dispatchStart = source.IndexOf("internal void Dispatch(", StringComparison.Ordinal);
        int abortStart = source.IndexOf("internal void Abort()", dispatchStart, StringComparison.Ordinal);
        string dispatchBlock = dispatchStart >= 0 && abortStart > dispatchStart ? source.Substring(dispatchStart, abortStart - dispatchStart) : string.Empty;
        int boxedInfoIndex = dispatchBlock.IndexOf("new EFT.Ballistics.DamageInfo(boxed)", StringComparison.Ordinal);
        int normalRecordIndex = dispatchBlock.IndexOf("RecordNormalCallback(info, part, amount)", StringComparison.Ordinal);
        int businessCallbackIndex = dispatchBlock.IndexOf("_callback?.Invoke(info, part, amount)", StringComparison.Ordinal);
        bool normalDispatchIsSeparate = boxedInfoIndex >= 0 && normalRecordIndex > boxedInfoIndex && businessCallbackIndex > normalRecordIndex
            && dispatchBlock.Contains("if (!_controlledSmoke) Spt5BeingHitActionBridge.RecordNormalCallback(info, part, amount)", StringComparison.Ordinal);
        int normalRecordStart = source.IndexOf("internal static void RecordNormalCallback(", StringComparison.Ordinal);
        int smokeRecordStart = source.IndexOf("internal static void RecordSmokeError(", normalRecordStart, StringComparison.Ordinal);
        string normalRecordBlock = normalRecordStart >= 0 && smokeRecordStart > normalRecordStart ? source.Substring(normalRecordStart, smokeRecordStart - normalRecordStart) : string.Empty;
        bool diagnosticCannotBlockOrPollute = normalRecordBlock.Contains("info.Player", StringComparison.Ordinal)
            && normalRecordBlock.Contains("observer.iPlayer", StringComparison.Ordinal)
            && normalRecordBlock.Contains("catch (Exception diagnosticError)", StringComparison.Ordinal)
            && normalRecordBlock.Contains("_normalDiagnosticError", StringComparison.Ordinal)
            && !normalRecordBlock.Contains("RecordError(", StringComparison.Ordinal);
        bool snapshotSeparatesNormalAndSmoke = source.Contains("normalCallbacks = Volatile.Read(ref _normalCallbacks)", StringComparison.Ordinal)
            && source.Contains("lastNormalPart =", StringComparison.Ordinal)
            && source.Contains("lastNormalAmount =", StringComparison.Ordinal)
            && source.Contains("lastNormalAttackerBridgePointer =", StringComparison.Ordinal)
            && source.Contains("lastNormalAttackerPlayerPointer =", StringComparison.Ordinal)
            && source.Contains("normalDiagnosticError =", StringComparison.Ordinal)
            && source.Contains("smokeAttempts = Volatile.Read(ref _smokeAttempts)", StringComparison.Ordinal);
        Check("normal-callback-diagnostics-after-boxing-before-business-callback-and-isolated-from-smoke", normalDispatchIsSeparate && diagnosticCannotBlockOrPollute && snapshotSeparatesNormalAndSmoke,
            "Only normal Dispatch records count/part/amount and attacker bridge/iPlayer pointers after successful boxing, before invoking the original callback; pointer-read failures are caught into normalDiagnosticError and never call RecordError; smoke counters remain separate.");
        bool passed = checks.All(check => check.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new { command = "being-hit-action-method-info-self-test", passed, bridge = Path.GetFullPath(bridgePath), runtime = Path.GetFullPath(runtimePath), interop = Path.GetFullPath(interopPath), checks }, J));
        return passed ? 0 : 1;
    }
    private sealed record MethodInfoCheck(string Name, bool Passed, string Evidence);
}