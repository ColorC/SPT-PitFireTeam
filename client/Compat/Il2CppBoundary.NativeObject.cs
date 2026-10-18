namespace pitTeam.Compat
{
    internal static partial class Il2CppBoundary
    {
        public static T? CastNativeOrNull<T>(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? value)
            where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            if (value == null) return null;
            return value.TryCast<T>();
        }
        public static T? CastNativeOrNull<T>(Il2CppInterop.Runtime.InteropTypes.IIl2CppObjectBase? value)
            where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            if (value == null) return null;
            return value.TryCast<T>();
        }
        public static System.Collections.Generic.IEnumerable<UnityEngine.Transform> EnumerateNativeChildren(UnityEngine.Transform parent)
        {
            if (parent == null) throw new System.ArgumentNullException(nameof(parent));
            return EnumerateNativeChildrenCore(parent);
        }

        private static System.Collections.Generic.IEnumerable<UnityEngine.Transform> EnumerateNativeChildrenCore(UnityEngine.Transform parent)
        {
            int childCount = parent.childCount;
            for (int index = 0; index < childCount; index++)
                yield return parent.GetChild(index);
        }

        public static bool SameNativeObject(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? a, Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? b)
        {
            if (a == null || b == null) return false;
            var pointer = a.Pointer;
            return pointer != System.IntPtr.Zero && pointer == b.Pointer;
        }

        public static EFT.InventoryLogic.DefaultItemContext CreateDefaultItemContext(
            EFT.InventoryLogic.Item item,
            EFT.InventoryLogic.EItemViewType viewType)
        {
            if (item == null) throw new System.ArgumentNullException(nameof(item));

            var context = new EFT.InventoryLogic.DefaultItemContext(item, viewType);
            var nativeContext = CastNativeOrNull<EFT.InventoryLogic.ItemContext>(context);
            if (context == null || context.Pointer == System.IntPtr.Zero || nativeContext == null || !SameNativeObject(context, nativeContext))
                throw new System.InvalidOperationException("DefaultItemContext constructor did not produce a native ItemContext.");
            if (!SameNativeObject(item, context.Item))
                throw new System.InvalidOperationException("DefaultItemContext did not retain the requested root item.");

            return context;
        }


    }
}