using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using EFT;
using HarmonyLib;

namespace pitTeam.Compat
{
    internal static class NativeConstructorPatchTargets
    {
        private const string CommonAssemblyName = "Il2CppInterop.Common";
        private const string ResolverTypeName = "Il2CppInterop.Common.Il2CppInteropUtils";
        private const string ResolverMethodName = "GetIl2CppMethodInfoPointerFieldForGeneratedMethod";
        private const string LocalGamePointerField = "NativeMethodInfoPtr__ctor_Public_Void_0";
        private const string AIDataPointerField = "NativeMethodInfoPtr__ctor_Public_Void_BotOwner_Player_0";

        private static readonly object InstallLock = new object();
        private static Harmony? _harmony;
        private static bool _installed;
        private static int _localGameCallbacks;
        private static int _aiDataCallbacks;
        private static int _localGameResolverRequests;
        private static int _aiDataResolverRequests;
        private static int _localGameResolverResolutions;
        private static int _aiDataResolverResolutions;
        private static long _lastLocalGameInstance;
        private static long _lastAIDataInstance;
        private static long _lastLocalGameMethodPointer;
        private static long _lastAIDataMethodPointer;
        private static string? _lastLocalGamePointerReadError;
        private static string? _lastAIDataPointerReadError;
        private static string? _lastLocalGameResolverError;
        private static string? _lastAIDataResolverError;

        private static bool ProbeEnabled =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT"));

        internal static void EnsureInstalled()
        {
            lock (InstallLock)
            {
                if (_installed)
                {
                    return;
                }

                Assembly commonAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetName().Name == CommonAssemblyName)
                    ?? Assembly.Load(CommonAssemblyName);
                Type resolverType = commonAssembly.GetType(ResolverTypeName, true)!;
                MethodInfo resolver = resolverType.GetMethod(
                    ResolverMethodName,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(MethodBase) },
                    null
                ) ?? throw new MissingMethodException(resolverType.FullName, ResolverMethodName);
                MethodInfo prefix = typeof(NativeConstructorPatchTargets).GetMethod(
                    nameof(ResolveNativeConstructorPointerField),
                    BindingFlags.NonPublic | BindingFlags.Static
                ) ?? throw new MissingMethodException(
                    typeof(NativeConstructorPatchTargets).FullName,
                    nameof(ResolveNativeConstructorPointerField)
                );

                _harmony = new Harmony("pitTeam.native-constructor-pointer-resolver");
                _harmony.Patch(resolver, prefix: new HarmonyMethod(prefix));
                _installed = true;
            }
        }

        private static bool ResolveNativeConstructorPointerField(MethodBase method, ref FieldInfo __result)
        {
            string? requiredField = GetRequiredPointerField(method);
            if (requiredField == null)
            {
                return true;
            }

            bool localGame = requiredField == LocalGamePointerField;
            if (ProbeEnabled)
            {
                if (localGame)
                {
                    Interlocked.Increment(ref _localGameResolverRequests);
                }
                else
                {
                    Interlocked.Increment(ref _aiDataResolverRequests);
                }
            }

            try
            {
                Type declaringType = method.DeclaringType
                    ?? throw new InvalidOperationException(
                        "Native constructor target has no declaring type: " + method
                    );
                FieldInfo[] fields = declaringType
                    .GetFields(
                        BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.DeclaredOnly
                    )
                    .Where(field => field.Name == requiredField)
                    .ToArray();
                if (fields.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Expected exactly one native constructor MethodInfo field "
                            + declaringType.FullName
                            + "."
                            + requiredField
                            + "; found "
                            + fields.Length
                            + "."
                    );
                }

                FieldInfo pointerField = fields[0];
                if (!pointerField.IsStatic || pointerField.FieldType != typeof(IntPtr))
                {
                    throw new InvalidOperationException(
                        "Native constructor MethodInfo field has an invalid shape: "
                            + pointerField
                            + "; expected static System.IntPtr."
                    );
                }
                if (pointerField.GetValue(null) is not IntPtr pointer || pointer == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "Native constructor MethodInfo field is null or zero: "
                            + declaringType.FullName
                            + "."
                            + requiredField
                            + "."
                    );
                }

                __result = pointerField;
                if (ProbeEnabled)
                {
                    if (localGame)
                    {
                        Interlocked.Increment(ref _localGameResolverResolutions);
                    }
                    else
                    {
                        Interlocked.Increment(ref _aiDataResolverResolutions);
                    }
                }
                return false;
            }
            catch (Exception error)
            {
                if (ProbeEnabled)
                {
                    if (localGame)
                    {
                        _lastLocalGameResolverError = error.ToString();
                    }
                    else
                    {
                        _lastAIDataResolverError = error.ToString();
                    }
                }
                throw;
            }
        }

        private static string? GetRequiredPointerField(MethodBase method)
        {
            if (!method.IsConstructor
                || method.DeclaringType == null
                || method.DeclaringType.Assembly.GetName().Name != "Assembly-CSharp")
            {
                return null;
            }

            Type declaringType = method.DeclaringType;
            Type[] parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            if (declaringType == typeof(LocalGame) && parameters.Length == 0)
            {
                return LocalGamePointerField;
            }
            if (declaringType == typeof(AIData)
                && parameters.Length == 2
                && parameters[0] == typeof(BotOwner)
                && parameters[1] == typeof(Player))
            {
                return AIDataPointerField;
            }
            return null;
        }

        internal static void RecordLocalGameCallback(IntPtr instance)
        {
            if (!ProbeEnabled)
            {
                return;
            }

            Interlocked.Exchange(ref _lastLocalGameInstance, instance.ToInt64());
            Interlocked.Increment(ref _localGameCallbacks);
            RecordMethodPointer(typeof(LocalGame), LocalGamePointerField, true);
        }

        internal static void RecordAIDataCallback(IntPtr instance)
        {
            if (!ProbeEnabled)
            {
                return;
            }

            Interlocked.Exchange(ref _lastAIDataInstance, instance.ToInt64());
            Interlocked.Increment(ref _aiDataCallbacks);
            RecordMethodPointer(typeof(AIData), AIDataPointerField, false);
        }

        private static void RecordMethodPointer(Type declaringType, string fieldName, bool localGame)
        {
            try
            {
                FieldInfo field = declaringType.GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly
                ) ?? throw new MissingFieldException(declaringType.FullName, fieldName);
                if (field.FieldType != typeof(IntPtr) || !field.IsStatic)
                {
                    throw new InvalidOperationException("Expected static System.IntPtr field: " + field);
                }
                if (field.GetValue(null) is not IntPtr pointer)
                {
                    throw new InvalidOperationException(
                        "Native MethodInfo field did not contain System.IntPtr: " + field
                    );
                }

                if (localGame)
                {
                    Interlocked.Exchange(ref _lastLocalGameMethodPointer, pointer.ToInt64());
                }
                else
                {
                    Interlocked.Exchange(ref _lastAIDataMethodPointer, pointer.ToInt64());
                }
            }
            catch (Exception error)
            {
                if (localGame)
                {
                    _lastLocalGamePointerReadError = error.ToString();
                }
                else
                {
                    _lastAIDataPointerReadError = error.ToString();
                }
            }
        }

        internal static object Capture()
        {
            return new
            {
                resolverInstalled = _installed,
                localGame = new
                {
                    target = "EFT.LocalGame::.ctor()",
                    pointerField = LocalGamePointerField,
                    resolverRequests = Volatile.Read(ref _localGameResolverRequests),
                    resolverResolutions = Volatile.Read(ref _localGameResolverResolutions),
                    resolverError = _lastLocalGameResolverError,
                    callbacks = Volatile.Read(ref _localGameCallbacks),
                    lastInstancePointer = new IntPtr(Interlocked.Read(ref _lastLocalGameInstance)).ToString("X"),
                    nativeMethodInfoPointer = new IntPtr(Interlocked.Read(ref _lastLocalGameMethodPointer)).ToString("X"),
                    pointerReadError = _lastLocalGamePointerReadError
                },
                aiData = new
                {
                    target = "EFT.AIData::.ctor(EFT.BotOwner,EFT.Player)",
                    pointerField = AIDataPointerField,
                    resolverRequests = Volatile.Read(ref _aiDataResolverRequests),
                    resolverResolutions = Volatile.Read(ref _aiDataResolverResolutions),
                    resolverError = _lastAIDataResolverError,
                    callbacks = Volatile.Read(ref _aiDataCallbacks),
                    lastInstancePointer = new IntPtr(Interlocked.Read(ref _lastAIDataInstance)).ToString("X"),
                    nativeMethodInfoPointer = new IntPtr(Interlocked.Read(ref _lastAIDataMethodPointer)).ToString("X"),
                    pointerReadError = _lastAIDataPointerReadError
                }
            };
        }
    }
}