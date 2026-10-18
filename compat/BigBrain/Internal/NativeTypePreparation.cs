using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace DrakiaXYZ.BigBrain.Internal
{
    internal static class NativeTypePreparation
    {
        // Abstract generic ancestors can have lazily populated method tables.
        // Use the exported native metadata API before ClassInjector walks those tables.
        internal static void PrepareHierarchy<T>()
        {
            IntPtr type = Il2CppClassPointerStore<T>.NativeClassPtr;
            if (type == IntPtr.Zero) throw new InvalidOperationException("Missing native class for " + typeof(T));
            var seen = new HashSet<IntPtr>();
            while (type != IntPtr.Zero)
            {
                if (!seen.Add(type)) throw new InvalidOperationException("Native class hierarchy contains a cycle.");
                IntPtr iterator = IntPtr.Zero;
                int count = 0;
                while (IL2CPP.il2cpp_class_get_methods(type, ref iterator) != IntPtr.Zero) count++;
                string name = Marshal.PtrToStringUTF8(IL2CPP.il2cpp_class_get_name(type));
                BigBrainPlugin.BigBrainLogger.LogInfo("Prepared native method table: " + name + " methods=" + count);
                type = IL2CPP.il2cpp_class_get_parent(type);
            }
        }
    }
}
