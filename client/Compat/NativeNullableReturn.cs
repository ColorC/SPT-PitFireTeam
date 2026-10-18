using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace pitTeam.Compat
{
    internal static class NativeNullableReturn
    {
        private static readonly ConcurrentDictionary<(Type Type, string Name, string Field, Type Value), IntPtr> Methods = new();
        internal static T InvokeNoArgument<T>(Il2CppObjectBase target, Type type, string name, string field, out bool hasValue)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(type);
            if (!type.IsInstanceOfType(target)) throw new ArgumentException("Native receiver type mismatch.", nameof(target));
            IntPtr method = Methods.GetOrAdd((type, name, field, typeof(T)), static k => Resolve(k.Type, k.Name, k.Field, k.Value));
            IntPtr error = IntPtr.Zero;
            hasValue = false;
            try
            {
                IntPtr result;
                unsafe { result = IL2CPP.il2cpp_runtime_invoke(method, IL2CPP.Il2CppObjectBaseToPtrNotNull(target), null, ref error); }
                return ReadBoxedValue<T>(result, error, out hasValue);
            }
            finally { GC.KeepAlive(target); }
        }

        internal static T ReadBoxedValue<T>(IntPtr result, IntPtr error, out bool hasValue)
        {
            hasValue = false;
            var initialOutcome = NativeNullableReturnPolicy.Classify(result.ToInt64(), error.ToInt64(), 0, 0);
            if (initialOutcome == NativeNullableReturnOutcome.NativeException) Il2CppException.RaiseExceptionIfNecessary(error);
            if (initialOutcome == NativeNullableReturnOutcome.None) return default;
            RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
            IntPtr expected = Il2CppClassPointerStore<T>.NativeClassPtr;
            IntPtr actual = IL2CPP.il2cpp_object_get_class(result);
            if (NativeNullableReturnPolicy.Classify(result.ToInt64(), error.ToInt64(), actual.ToInt64(), expected.ToInt64()) != NativeNullableReturnOutcome.BoxedValue)
                throw new NotSupportedException("Native nullable return is not boxed " + typeof(T).FullName + ".");
            T value = IL2CPP.PointerToValueGeneric<T>(result, false, true);
            hasValue = true;
            return value;
        }

        internal static T? InvokeNoArgumentNullable<T>(Il2CppObjectBase target, Type type, string name, string field) where T : struct
        {
            T value = InvokeNoArgument<T>(target, type, name, field, out bool present);
            return present ? value : (T?)null;
        }

        private static IntPtr Resolve(Type type, string name, string fieldName, Type valueType)
        {
            int matches = 0;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type result = method.ReturnType;
                if (method.Name == name && method.GetParameters().Length == 0 && result.IsGenericType &&
                    result.GetGenericTypeDefinition() == typeof(Il2CppSystem.Nullable<>) && result.GetGenericArguments()[0] == valueType) matches++;
            }
            if (matches != 1) throw new MissingMethodException(type.FullName, name + "() -> Nullable<" + valueType.FullName + ">");
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || !field.IsStatic || field.FieldType != typeof(IntPtr)) throw new MissingFieldException(type.FullName, fieldName);
            if (field.GetValue(null) is not IntPtr pointer || pointer == IntPtr.Zero) throw new InvalidOperationException("Native method info uninitialized: " + fieldName);
            return pointer;
        }
    }
}
