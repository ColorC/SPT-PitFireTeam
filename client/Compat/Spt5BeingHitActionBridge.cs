using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using EFT;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Injection;
namespace pitTeam.Compat
{
    internal sealed class Spt5BeingHitActionSubscription : IDisposable
    {
        private Player? _player;
        private readonly Il2CppSystem.Action<EFT.Ballistics.DamageInfo, EBodyPart, float> _token;
        private Il2CppObjectBase? _reference;
        private Delegate? _thunkRoot;
        private Action<EFT.Ballistics.DamageInfo, EBodyPart, float>? _callback;
        private int _disposed;
        private readonly bool _controlledSmoke;
        private int _attached;
        internal Spt5BeingHitActionSubscription(Player? player, Il2CppSystem.Action<EFT.Ballistics.DamageInfo, EBodyPart, float> token, Il2CppObjectBase reference, Delegate thunk, Action<EFT.Ballistics.DamageInfo, EBodyPart, float> callback, bool controlledSmoke)
        { _player = player; _token = token; _reference = reference; _thunkRoot = thunk; _callback = callback; _controlledSmoke = controlledSmoke; }
        internal Il2CppSystem.Action<EFT.Ballistics.DamageInfo, EBodyPart, float> Token => _token;
        internal void MarkAttached() { Volatile.Write(ref _attached, 1); Spt5BeingHitActionBridge.Attached(); }
        internal void Dispatch(IntPtr data, EBodyPart part, float amount)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            try
            {
                if (data == IntPtr.Zero) throw new InvalidOperationException("BeingHitAction supplied null DamageInfo data.");
                IntPtr boxed = IL2CPP.il2cpp_value_box(Spt5BeingHitActionBridge.DamageInfoClassPointer(), data);
                if (boxed == IntPtr.Zero) throw new InvalidOperationException("il2cpp_value_box returned null.");
                var info = new EFT.Ballistics.DamageInfo(boxed);
                if (Volatile.Read(ref _disposed) == 0)
                {
                    if (!_controlledSmoke) Spt5BeingHitActionBridge.RecordNormalCallback(info, part, amount);
                    _callback?.Invoke(info, part, amount);
                }
            }
            catch (Exception error) { if (_controlledSmoke) Spt5BeingHitActionBridge.RecordSmokeError(error); else Spt5BeingHitActionBridge.RecordError(error); }
        }
        internal void Abort()
        {
            Interlocked.Exchange(ref _disposed, 1);
            _callback = null; _reference = null; _thunkRoot = null; _player = null;
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                if (Interlocked.Exchange(ref _attached, 0) != 0)
                {
                    try { if (_player != null) _player.BeingHitAction -= _token; }
                    finally { Spt5BeingHitActionBridge.Detached(); }
                }
            }
            catch (Exception error) { if (_controlledSmoke) Spt5BeingHitActionBridge.RecordSmokeError(error); else Spt5BeingHitActionBridge.RecordError(error); }
            finally { _callback = null; _reference = null; _thunkRoot = null; _player = null; }
        }
    }
    internal static class Spt5BeingHitActionBridge
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RawThunk(IntPtr target, IntPtr damageInfoData, EBodyPart bodyPart, float amount, IntPtr methodInfo);
        private static int _attempts, _active, _errors;
        private static int _smokeAttempts, _smokeErrors;
        private static int _normalCallbacks, _lastNormalPart;
        private static float _lastNormalAmount;
        private static string? _lastNormalAttackerBridgePointer, _lastNormalAttackerPlayerPointer, _normalDiagnosticError;
        private static string? _firstError;
        private static string? _firstSmokeError;
        internal static bool SupportsRawByReferenceValue(bool windowsX64, bool nativeValueType, uint nativeSize) => Spt5BeingHitActionAbiGate.SupportsRawByReferenceValue(windowsX64, nativeValueType, nativeSize);
        internal static object GetProbeSnapshot() => new { supported = true, available = true, attempts = Volatile.Read(ref _attempts), active = Volatile.Read(ref _active), errors = Volatile.Read(ref _errors), firstError = Volatile.Read(ref _firstError), normalCallbacks = Volatile.Read(ref _normalCallbacks), lastNormalPart = Volatile.Read(ref _normalCallbacks) == 0 ? null : ((EBodyPart)Volatile.Read(ref _lastNormalPart)).ToString(), lastNormalAmount = Volatile.Read(ref _lastNormalAmount), lastNormalAttackerBridgePointer = Volatile.Read(ref _lastNormalAttackerBridgePointer), lastNormalAttackerPlayerPointer = Volatile.Read(ref _lastNormalAttackerPlayerPointer), normalDiagnosticError = Volatile.Read(ref _normalDiagnosticError), smokeAttempts = Volatile.Read(ref _smokeAttempts), smokeErrors = Volatile.Read(ref _smokeErrors), firstSmokeError = Volatile.Read(ref _firstSmokeError), abi = "Windows-x64 native ValueType passed by reference; il2cpp_value_box(class,data)", status = Volatile.Read(ref _errors) == 0 ? "ready" : "error" };
        internal static void RecordError(Exception error)
        {
            Interlocked.Increment(ref _errors); Interlocked.CompareExchange(ref _firstError, error.ToString(), null);
            pitTeam.Modules.Logger.LogError("[Spt5BeingHitActionBridge] " + error);
        }
        internal static void RecordNormalCallback(EFT.Ballistics.DamageInfo info, EBodyPart part, float amount)
        {
            Interlocked.Increment(ref _normalCallbacks);
            Volatile.Write(ref _lastNormalPart, (int)part);
            Volatile.Write(ref _lastNormalAmount, amount);
            Volatile.Write(ref _lastNormalAttackerBridgePointer, (string?)null);
            Volatile.Write(ref _lastNormalAttackerPlayerPointer, (string?)null);
            Volatile.Write(ref _normalDiagnosticError, (string?)null);
            try
            {
                EFT.IObserverToPlayerBridge? observer = info.Player;
                IntPtr bridgePointer = ReadNativeObjectPointer(observer);
                IntPtr playerPointer = observer == null ? IntPtr.Zero : ReadNativeObjectPointer(observer.iPlayer);
                Volatile.Write(ref _lastNormalAttackerBridgePointer, bridgePointer.ToString("X"));
                Volatile.Write(ref _lastNormalAttackerPlayerPointer, playerPointer.ToString("X"));
            }
            catch (Exception diagnosticError)
            {
                Volatile.Write(ref _normalDiagnosticError, diagnosticError.ToString());
            }
        }
        internal static void RecordSmokeError(Exception error)
        {
            Interlocked.Increment(ref _smokeErrors); Interlocked.CompareExchange(ref _firstSmokeError, error.ToString(), null);
            pitTeam.Modules.Logger.LogError("[Spt5BeingHitActionBridge controlled smoke] " + error);
        }
        internal static void Attached() => Interlocked.Increment(ref _active);
        internal static void Detached() => Interlocked.Decrement(ref _active);
        internal static IntPtr DamageInfoClassPointer() => NativeClassPointer(typeof(EFT.Ballistics.DamageInfo));
        internal static Spt5BeingHitActionSubscription Subscribe(Player player, Action<EFT.Ballistics.DamageInfo, EBodyPart, float> callback)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            Interlocked.Increment(ref _attempts);
            Spt5BeingHitActionSubscription? subscription = null;
            try
            {
                subscription = CreateSubscription(player, callback);
                player.BeingHitAction += subscription.Token;
                subscription.MarkAttached();
                return subscription;
            }
            catch (Exception error)
            {
                RecordError(error);
                if (subscription != null)
                {
                    try { player.BeingHitAction -= subscription.Token; }
                    catch (Exception detachError) { RecordError(detachError); }
                    subscription.Abort();
                }
                throw new InvalidOperationException("Spt5 BeingHitAction registration failed: " + error, error);
            }
        }
        private static Spt5BeingHitActionSubscription CreateSubscription(Player? player, Action<EFT.Ballistics.DamageInfo, EBodyPart, float> callback, bool controlledSmoke = false)
        {
            bool windowsX64 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IntPtr.Size == 8;
            bool nativeValueType = typeof(EFT.Ballistics.DamageInfo).BaseType == typeof(Il2CppSystem.ValueType);
            IntPtr damageClass = DamageInfoClassPointer();
            if (damageClass == IntPtr.Zero) throw new InvalidOperationException("DamageInfo NativeClassPtr is null.");
            uint alignment = 0;
            int size = IL2CPP.il2cpp_class_value_size(damageClass, ref alignment);
            if (!SupportsRawByReferenceValue(windowsX64, nativeValueType, checked((uint)size)))
                throw new PlatformNotSupportedException("Raw DamageInfo callback requires Windows x64 native ValueType size > 8; valueType=" + nativeValueType + ", actual size=" + size + ", alignment=" + alignment + ".");
            Type actionType = typeof(Il2CppSystem.Action<EFT.Ballistics.DamageInfo, EBodyPart, float>);
            IntPtr actionClass = NativeClassPointer(actionType);
            if (actionClass == IntPtr.Zero) throw new InvalidOperationException("Native Action3 class pointer is null.");
            var invokeInfo = NativeActionInvokeMethod(actionClass);
            ValidateInvoke(invokeInfo);
            Spt5BeingHitActionSubscription? subscription = null;
            RawThunk thunk = new RawThunk((_, data, part, amount, __) => subscription?.Dispatch(data, part, amount));
            IntPtr thunkPointer = Marshal.GetFunctionPointerForDelegate(thunk);
            object nativeMethod = NewNativeMethodInfo(thunkPointer);
            IntPtr methodInfoPointer = NativeStructPointer(nativeMethod);
            Il2CppObjectBase reference = CreateDelegateReference(thunk, methodInfoPointer);
            Il2CppSystem.Delegate native = CreateNativeDelegate(actionType, actionClass, invokeInfo, thunkPointer, methodInfoPointer, reference);
            var token = native.Cast<Il2CppSystem.Action<EFT.Ballistics.DamageInfo, EBodyPart, float>>();
            subscription = new Spt5BeingHitActionSubscription(player, token, reference, thunk, callback, controlledSmoke);
            return subscription;
        }
        internal static object RunControlledNativeSmoke(Player? samplePlayer)
        {
            Interlocked.Increment(ref _smokeAttempts);
            IntPtr data = IntPtr.Zero;
            bool playerReferenceReady = samplePlayer == null;
            string referenceMode = samplePlayer == null ? "zero-reference" : "actual-main-player";
            EFT.PlayerBridge? observerBridge = null;
            try
            {
                IntPtr classPointer = DamageInfoClassPointer();
                if (classPointer == IntPtr.Zero) throw new InvalidOperationException("DamageInfo NativeClassPtr is null.");
                uint alignment = 0;
                int size = IL2CPP.il2cpp_class_value_size(classPointer, ref alignment);
                if (size <= 8) throw new PlatformNotSupportedException("DamageInfo native value type is outside the large-value bridge gate.");
                data = Marshal.AllocHGlobal(size);
                Marshal.Copy(new byte[size], 0, data, size);
                IntPtr boxed = IL2CPP.il2cpp_value_box(classPointer, data);
                if (boxed == IntPtr.Zero) throw new InvalidOperationException("Could not box the zero-initialized native DamageInfo smoke value.");
                var sample = new EFT.Ballistics.DamageInfo(boxed);
                IntPtr expectedBridgePointer = IntPtr.Zero;
                IntPtr expectedPlayerPointer = IntPtr.Zero;
                if (samplePlayer != null)
                {
                    observerBridge = new EFT.PlayerBridge(samplePlayer);
                    if (observerBridge.Pointer == IntPtr.Zero) throw new InvalidOperationException("PlayerBridge(Player) produced a null native bridge object.");
                    sample.Player = observerBridge;
                    expectedBridgePointer = observerBridge.Pointer;
                    expectedPlayerPointer = samplePlayer.Pointer;
                    playerReferenceReady = true;
                }
                object result = RunControlledNativeSmoke(sample, expectedBridgePointer, expectedPlayerPointer, (EBodyPart)0, 17.25f, referenceMode);
                GC.KeepAlive(observerBridge);
                return result;
            }
            catch (Exception error)
            {
                RecordSmokeError(error);
                return new { supported = true, available = playerReferenceReady, passed = false, referenceMode, error = error.ToString(), healthChanged = false, spawned = false };
            }
            finally { if (data != IntPtr.Zero) Marshal.FreeHGlobal(data); }
        }
        internal static object RunControlledNativeSmoke(EFT.Ballistics.DamageInfo sample, IntPtr expectedBridgePointer, IntPtr expectedPlayerPointer, EBodyPart expectedPart, float expectedAmount, string referenceMode)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            int smokeErrorsBefore = Volatile.Read(ref _smokeErrors);
            int callbacks = 0; IntPtr actualBridgePointer = IntPtr.Zero; IntPtr actualPlayerPointer = IntPtr.Zero; EBodyPart actualPart = default; float actualAmount = 0;
            Action<EFT.Ballistics.DamageInfo, EBodyPart, float> callback = (info, part, amount) => { callbacks++; var observer = info.Player; actualBridgePointer = ReadNativeObjectPointer(observer); actualPlayerPointer = observer == null ? IntPtr.Zero : ReadNativeObjectPointer(observer.iPlayer); actualPart = part; actualAmount = amount; };
            Spt5BeingHitActionSubscription subscription = CreateSubscription(null, callback, controlledSmoke: true);
            try
            {
                subscription.Token.Invoke(sample, expectedPart, expectedAmount);
                bool valuesMatch = callbacks == 1 && actualBridgePointer == expectedBridgePointer && actualPlayerPointer == expectedPlayerPointer && actualPart == expectedPart && actualAmount.Equals(expectedAmount);
                subscription.Dispose();
                subscription.Token.Invoke(sample, expectedPart, expectedAmount);
                bool lateCallSuppressed = callbacks == 1;
                int smokeErrorCount = Volatile.Read(ref _smokeErrors) - smokeErrorsBefore;
                return new { supported = true, available = true, passed = valuesMatch && lateCallSuppressed && smokeErrorCount == 0, callbacks, smokeErrors = smokeErrorCount, firstSmokeError = smokeErrorCount == 0 ? null : Volatile.Read(ref _firstSmokeError), actualBridgePointer = actualBridgePointer.ToString(), expectedBridgePointer = expectedBridgePointer.ToString(), actualPlayerPointer = actualPlayerPointer.ToString(), expectedPlayerPointer = expectedPlayerPointer.ToString(), actualPart = actualPart.ToString(), expectedPart = expectedPart.ToString(), actualAmount, expectedAmount, lateCallSuppressed, referenceMode, healthChanged = false, spawned = false };
            }
            catch (Exception error)
            {
                subscription.Dispose(); RecordSmokeError(error);
                return new { supported = true, available = true, passed = false, callbacks, actualBridgePointer = actualBridgePointer.ToString(), expectedBridgePointer = expectedBridgePointer.ToString(), actualPlayerPointer = actualPlayerPointer.ToString(), expectedPlayerPointer = expectedPlayerPointer.ToString(), actualPart = actualPart.ToString(), expectedPart = expectedPart.ToString(), actualAmount, expectedAmount, lateCallSuppressed = false, referenceMode, healthChanged = false, spawned = false, error = error.ToString() };
            }
        }
        private static IntPtr ReadNativeObjectPointer(object? instance)
        {
            if (instance == null) return IntPtr.Zero;
            PropertyInfo? property = instance.GetType().GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null) return (IntPtr)(property.GetValue(instance) ?? IntPtr.Zero);
            FieldInfo? field = instance.GetType().GetField("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? IntPtr.Zero : (IntPtr)(field.GetValue(instance) ?? IntPtr.Zero);
        }
        private static IntPtr NativeClassPointer(Type type)
        {
            Type store = typeof(Il2CppClassPointerStore<>).MakeGenericType(type);
            FieldInfo field = store.GetField("NativeClassPtr", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingFieldException(store.FullName, "NativeClassPtr");
            return (IntPtr)(field.GetValue(null) ?? IntPtr.Zero);
        }
        private static Il2CppSystem.Reflection.MethodInfo NativeActionInvokeMethod(IntPtr actionClass)
        {
            IntPtr nativeTypeHandle = IL2CPP.il2cpp_class_get_type(actionClass);
            if (nativeTypeHandle == IntPtr.Zero) throw new InvalidOperationException("Action3 native type handle is null.");
            Il2CppSystem.Type actionNativeType = Il2CppSystem.Type.internal_from_handle(nativeTypeHandle);
            Il2CppSystem.Reflection.MethodInfo invoke = actionNativeType.GetMethod("Invoke");
            if (invoke == null || invoke.Pointer == IntPtr.Zero) throw new InvalidOperationException("Typed Action3.Invoke reflection method is unavailable.");
            return invoke;
        }
        private static void ValidateInvoke(Il2CppSystem.Reflection.MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters == null || parameters.Count != 3 || parameters[0].ParameterType.FullName != "EFT.Ballistics.DamageInfo" || parameters[1].ParameterType.FullName != typeof(EBodyPart).FullName || parameters[2].ParameterType.FullName != "System.Single")
                throw new InvalidOperationException("Native Action3.Invoke signature drifted.");
        }
        private static object NewNativeMethodInfo(IntPtr function)
        {
            Assembly runtime = typeof(IL2CPP).Assembly;
            Type factoryType = runtime.GetType("Il2CppInterop.Runtime.Runtime.UnityVersionHandler", true)!;
            object method = factoryType.GetMethod("NewMethod", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
            Type iface = runtime.GetType("Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo.INativeMethodInfoStruct", true)!;
            Marshal.WriteIntPtr(RefAddress(iface, "MethodPointer", method), function);
            Marshal.WriteByte(RefAddress(iface, "ParametersCount", method), 3);
            Marshal.WriteInt16(RefAddress(iface, "Slot", method), -1);
            iface.GetProperty("IsMarshalledFromNative")!.SetValue(method, true);
            return method;
        }
        private static IntPtr RefAddress(Type iface, string property, object instance)
        {
            MethodInfo getter = iface.GetProperty(property)!.GetMethod!;
            var dynamicMethod = new DynamicMethod("Spt5NativeRef_" + property, typeof(IntPtr), new[] { typeof(object) }, typeof(Spt5BeingHitActionBridge).Module, true);
            ILGenerator il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, iface); il.Emit(OpCodes.Callvirt, getter); il.Emit(OpCodes.Conv_I); il.Emit(OpCodes.Ret);
            IntPtr address = ((Func<object, IntPtr>)dynamicMethod.CreateDelegate(typeof(Func<object, IntPtr>)))(instance);
            if (address == IntPtr.Zero) throw new InvalidOperationException("Native method-info ref " + property + " is null.");
            return address;
        }
        private static IntPtr NativeStructPointer(object method)
        {
            Type iface = typeof(IL2CPP).Assembly.GetType("Il2CppInterop.Runtime.Runtime.INativeStruct", true)!;
            IntPtr pointer = (IntPtr)(iface.GetProperty("Pointer")!.GetValue(method) ?? IntPtr.Zero);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("Native method-info pointer is null.");
            return pointer;
        }
        private static Il2CppObjectBase CreateDelegateReference(Delegate thunk, IntPtr methodInfo)
        {
            Assembly runtime = typeof(IL2CPP).Assembly;
            Type referenceType = runtime.GetType("Il2CppInterop.Runtime.DelegateSupport+Il2CppToMonoDelegateReference", true)!;
            Type store = typeof(Il2CppClassPointerStore<>).MakeGenericType(referenceType);
            FieldInfo classPointer = store.GetField("NativeClassPtr", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            if ((IntPtr)(classPointer.GetValue(null) ?? IntPtr.Zero) == IntPtr.Zero) ClassInjector.RegisterTypeInIl2Cpp(referenceType);
            ConstructorInfo constructor = referenceType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Delegate), typeof(IntPtr) }, null)!;
            return (Il2CppObjectBase)constructor.Invoke(new object[] { thunk, methodInfo });
        }
        private static Il2CppSystem.Delegate CreateNativeDelegate(Type actionType, IntPtr actionClass, Il2CppSystem.Reflection.MethodInfo invokeInfo, IntPtr function, IntPtr nativeInfo, Il2CppObjectBase reference)
        {
            Assembly runtime = typeof(IL2CPP).Assembly;
            Type handler = runtime.GetType("Il2CppInterop.Runtime.Runtime.UnityVersionHandler", true)!;
            bool useConstructor = (bool)handler.GetProperty("MustUseDelegateConstructor", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            Il2CppSystem.Delegate result;
            if (useConstructor)
            {
                object instance = Activator.CreateInstance(actionType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { reference.Cast<Il2CppSystem.Object>(), nativeInfo }, null)!;
                result = ((Il2CppObjectBase)instance).Cast<Il2CppSystem.Delegate>();
            }
            else
            {
                IntPtr pointer = IL2CPP.il2cpp_object_new(actionClass);
                if (pointer == IntPtr.Zero) throw new InvalidOperationException("il2cpp_object_new failed for Action3.");
                result = new Il2CppSystem.Delegate(pointer);
            }
            result.method_ptr = function; result.method_info = invokeInfo; result.method = nativeInfo; result.m_target = reference.Cast<Il2CppSystem.Object>();
            if (useConstructor) { result.invoke_impl = result.method_ptr; result.method_code = reference.Pointer; }
            return result;
        }
    }
}