using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace pitTeam.Compat
{
    internal static class NativeAiGeometry
    {
        private const string CrossPointField = "NativeMethodInfoPtr_GetCrossPoint_Public_Static_Nullable_1_Vector3_Vector3_Vector3_Vector3_Vector3_0";
        private static readonly Lazy<IntPtr> CrossPointMethod = new(ResolveCrossPoint);
        private static long calls, present, empty, errors;

        private static IntPtr ResolveCrossPoint()
        {
            Type type = typeof(AIUtility);
            var signature = new[] { typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(Vector3) };
            MethodInfo method = type.GetMethod(nameof(AIUtility.GetCrossPoint), BindingFlags.Public | BindingFlags.Static, null, signature, null)
                ?? throw new MissingMethodException(type.FullName, "GetCrossPoint(Vector3,Vector3,Vector3,Vector3)");
            if (method.ReturnType != typeof(Il2CppSystem.Nullable<Vector3>))
                throw new NotSupportedException("Unexpected native GetCrossPoint return type.");
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            FieldInfo field = type.GetField(CrossPointField, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingFieldException(type.FullName, CrossPointField);
            if (field.FieldType != typeof(IntPtr) || field.GetValue(null) is not IntPtr pointer || pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native GetCrossPoint method info is unavailable.");
            return pointer;
        }

        internal static unsafe bool SegmentsCross(Vector3 a1, Vector3 b1, Vector3 a2, Vector3 b2)
        {
            bool observe = Spt5RuntimeInstallProbe.Enabled;
            if (observe) calls++;
            try
            {
                IntPtr* arguments = stackalloc IntPtr[4];
                arguments[0] = (IntPtr)(&a1); arguments[1] = (IntPtr)(&b1);
                arguments[2] = (IntPtr)(&a2); arguments[3] = (IntPtr)(&b2);
                IntPtr error = IntPtr.Zero;
                // runtime_invoke boxes Nullable<T> as null or T. The generated wrapper
                // constructs Nullable<Vector3>(null), throwing for a valid non-intersection.
                IntPtr result = IL2CPP.il2cpp_runtime_invoke(CrossPointMethod.Value, IntPtr.Zero, (void**)arguments, ref error);
                NativeNullableReturn.ReadBoxedValue<Vector3>(result, error, out bool hasValue);
                if (observe) { if (hasValue) present++; else empty++; }
                return hasValue;
            }
            catch { if (observe) errors++; throw; }
        }

        public static object GetProbeSnapshot() => new { calls, present, empty, errors };

        public static object RunProbe()
        {
            Spt5RuntimeRaidProbe.RequireShadow();
            ManagedFramePump.RequireOwnerThread();
            bool crossing = SegmentsCross(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 0, 1));
            bool disjoint = SegmentsCross(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, -1), new Vector3(2, 0, 1));
            bool parallel = SegmentsCross(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(-1, 0, 1), new Vector3(1, 0, 1));
            return new { passed = crossing && !disjoint && !parallel, crossing, disjoint, parallel,
                method = "actual AIUtility.GetCrossPoint native method; no world or AI mutation", state = GetProbeSnapshot() };
        }
    }
}
