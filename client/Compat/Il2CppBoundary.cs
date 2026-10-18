global using pitTeam.Compat;

using System;
using System.Collections.Generic;
using EFT.InventoryLogic;

namespace pitTeam.Compat
{
    internal sealed class UnsupportedNativeItemComponentsInterfaceException : InvalidOperationException
    {
        public UnsupportedNativeItemComponentsInterfaceException()
            : base("Native Item.Components could not be cast to non-generic IList.")
        {
        }
    }
    internal static partial class Il2CppBoundary
    {

        public static void GetReachableItemsOfTypeNonAlloc<T>(
            this InventoryController controller,
            System.Collections.Generic.List<T> destination,
            System.Func<T, bool> predicate = null) where T : Item
        {
            ArgumentNullException.ThrowIfNull(destination);
            var native = new Il2CppSystem.Collections.Generic.List<T>();
            controller.GetReachableItemsOfTypeNonAlloc<T>(native, null);
            foreach (T value in native.AsManaged())
            {
                if (predicate == null || predicate(value)) destination.Add(value);
            }
        }
        public static void GetAcceptableItemsNonAlloc<T>(
            this InventoryController controller,
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<EquipmentSlot> equipmentSlots,
            System.Collections.Generic.List<T> destination,
            System.Predicate<T> predicate = null,
            System.Predicate<ContainerCollection> goDeeperPredicate = null) where T : Item
        {
            ArgumentNullException.ThrowIfNull(destination);
            var native = new Il2CppSystem.Collections.Generic.List<T>();
            controller.GetAcceptableItemsNonAlloc<T>(equipmentSlots, native, null, null);
            foreach (T value in native.AsManaged())
            {
                if (predicate == null || predicate(value)) destination.Add(value);
            }
        }
        public static void GetAcceptableItemsNonAllocManaged<T>(
            InventoryController controller,
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<EquipmentSlot> equipmentSlots,
            System.Collections.Generic.List<T> destination,
            System.Func<T, bool> predicate = null,
            System.Predicate<ContainerCollection> goDeeperPredicate = null) where T : Item
        {
            ArgumentNullException.ThrowIfNull(destination);
            var native = new Il2CppSystem.Collections.Generic.List<T>();
            controller.GetAcceptableItemsNonAlloc<T>(equipmentSlots, native, null, null);
            foreach (T value in native.AsManaged())
            {
                if (predicate == null || predicate(value)) destination.Add(value);
            }
        }

        public static IEnumerable<T> GetComponentsFromMods<T>(Il2CppSystem.Collections.Generic.IEnumerable<Mod> mods) where T : class
        {
            ArgumentNullException.ThrowIfNull(mods);
            var native = new Il2CppSystem.Collections.Generic.List<Item>();
            foreach (Mod mod in mods)
            {
                if (mod != null) native.Add(mod);
            }
            return native.GetComponents<T>().AsManaged();
        }
        public static void GetAllItemsNonAlloc<T>(this Item item, System.Collections.Generic.List<T> destination, bool onlyMerged = false, bool includeParent = true) where T : Item
        {
            ArgumentNullException.ThrowIfNull(destination);
            var native = new Il2CppSystem.Collections.Generic.List<T>();
            item.GetAllItemsNonAlloc(native, onlyMerged, includeParent);
            foreach (T value in native.AsManaged()) destination.Add(value);
        }

        public static void GetAllAssembledItems<T>(this Item item, System.Collections.Generic.List<T> destination) where T : Item
        {
            ArgumentNullException.ThrowIfNull(destination);
            var native = new Il2CppSystem.Collections.Generic.List<T>();
            item.GetAllAssembledItems(native);
            foreach (T value in native.AsManaged()) destination.Add(value);
        }

        public static void GetAllItemsFromCollectionNonAlloc(this InventoryEquipment equipment, System.Collections.Generic.List<Item> destination)
        {
            ArgumentNullException.ThrowIfNull(destination);
            var native = new Il2CppSystem.Collections.Generic.List<Item>();
            equipment.GetAllItemsFromCollectionNonAlloc(native);
            foreach (Item value in native.AsManaged()) destination.Add(value);
        }

        // Adapt enumeration only; callers choose explicitly whether to take a snapshot.
        public static IEnumerable<T> AsManaged<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return Enumerate(source);
        }

        private static IEnumerable<T> Enumerate<T>(Il2CppSystem.Collections.Generic.IEnumerable<T> source)
        {
            var enumerator = source.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public static Il2CppSystem.Collections.Generic.List<T> ToNativeList<T>(this IEnumerable<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var result = new Il2CppSystem.Collections.Generic.List<T>();
            foreach (T item in source) result.Add(item);
            return result;
        }

        public static Il2CppSystem.Collections.Generic.HashSet<T> ToNativeHashSet<T>(this IEnumerable<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var result = new Il2CppSystem.Collections.Generic.HashSet<T>();
            foreach (T item in source) result.Add(item);
            return result;
        }
        // Use the non-generic object accessor: the generic enumerator projects interface
        // elements through PointerToValueGeneric<T> in the generated IL2CPP wrapper.
        public static T? GetItemComponentOrNull<T>(this Item item)
            where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            if (item == null) return null;
            var components = item.Components;
            if (components == null) return null;
            var nativeComponents = components.TryCast<Il2CppSystem.Collections.IList>();
            if (nativeComponents == null)
                throw new UnsupportedNativeItemComponentsInterfaceException();
            for (int index = 0; index < nativeComponents.Count; index++)
            {
                var component = nativeComponents[index];
                var match = component?.TryCast<T>();
                if (match != null) return match;
            }
            return null;
        }
        public static T? ToManagedNullable<T>(this Il2CppSystem.Nullable<T> value) where T : struct =>
            value != null && value != null && value.HasValue ? value.Value : (T?)null;

        public static Il2CppSystem.Nullable<T> ToNativeNullable<T>(this T? value) where T : struct =>
            value.HasValue ? new Il2CppSystem.Nullable<T>(value.Value) : new Il2CppSystem.Nullable<T>();

        public static Il2CppSystem.Action<T> ToIl2CppAction<T>(this System.Action<T> action) =>
            (Il2CppSystem.Action<T>)action;

        public static Il2CppSystem.Func<T1, T2, T3, TResult> ToIl2CppFunc<T1, T2, T3, TResult>(
            this System.Func<T1, T2, T3, TResult> func) =>
            (Il2CppSystem.Func<T1, T2, T3, TResult>)func;
        // Registration must precede Il2CppType.Of<T>() for managed injected components.
        public static void EnsureInjectedComponentRegistered<T>() where T : UnityEngine.Component
        {
            var managedType = typeof(T);
            if (!Il2CppInterop.Runtime.Injection.ClassInjector.IsTypeRegisteredInIl2Cpp(managedType))
                Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp(managedType);
        }

        public static T AddComponentViaIl2CppType<T>(this UnityEngine.GameObject gameObject)
            where T : UnityEngine.Component
        {
            if (gameObject == null) return null;
            EnsureInjectedComponentRegistered<T>();
            var nativeType = Il2CppInterop.Runtime.Il2CppType.Of<T>();
            var nativeComponent = gameObject.AddComponent(nativeType);
            return nativeComponent == null ? null : nativeComponent.TryCast<T>();
        }

        // Use the non-generic Unity overloads for injected managed MonoBehaviours.
        public static T GetComponentViaIl2CppType<T>(this UnityEngine.Component component)
            where T : UnityEngine.Component
        {
            if (component == null) return null;
            EnsureInjectedComponentRegistered<T>();
            var nativeType = Il2CppInterop.Runtime.Il2CppType.Of<T>();
            var nativeComponent = component.GetComponent(nativeType);
            return nativeComponent == null ? null : nativeComponent.TryCast<T>();
        }

        public static System.Collections.Generic.List<T> FindObjectsOfTypeAllViaIl2CppType<T>()
            where T : UnityEngine.Component
        {
            var result = new System.Collections.Generic.List<T>();
            EnsureInjectedComponentRegistered<T>();
            var nativeType = Il2CppInterop.Runtime.Il2CppType.Of<T>();
            var nativeObjects = UnityEngine.Resources.FindObjectsOfTypeAll(nativeType);
            if (nativeObjects == null) return result;

            foreach (UnityEngine.Object nativeObject in nativeObjects)
            {
                if (nativeObject == null) continue;
                var component = nativeObject.TryCast<T>();
                if (component != null) result.Add(component);
            }
            return result;
        }
    }
}
