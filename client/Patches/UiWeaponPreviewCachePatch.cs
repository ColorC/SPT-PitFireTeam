using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimationSystem;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using UnityEngine;

namespace pitTeam.Patches
{
    internal static class UiWeaponPreviewCache
    {
        internal sealed class Context
        {
            internal PlayerModelLoader Loader;
            internal Item Weapon;
            internal bool AnimateWeapon;
            internal int Layer;
        }

        [ThreadStatic] internal static Context Current;
        internal static bool ScopeReady { get; private set; }
        private static readonly Queue<object> Observations = new Queue<object>();

        internal static bool VerifyScopeRegistration()
        {
            var target = AccessTools.Method(typeof(PlayerModelLoader), nameof(PlayerModelLoader.CreateWeapon),
                new[] { typeof(float), typeof(bool), typeof(Item), typeof(int) });
            var info = Harmony.GetPatchInfo(target);
            var prefixes = info?.Prefixes.Where(p => p.PatchMethod.DeclaringType == typeof(UiWeaponPreviewContextPatch)).ToArray();
            var finalizers = info?.Finalizers.Where(p => p.PatchMethod.DeclaringType == typeof(UiWeaponPreviewContextPatch)).ToArray();
            ScopeReady = prefixes?.Length == 1 && finalizers?.Length == 1 && prefixes[0].owner == finalizers[0].owner;
            if (!ScopeReady) Current = null;
            return ScopeReady;
        }

        private static void Record(object value)
        {
            if (!pitTeam.Compat.Spt5RuntimeInstallProbe.Enabled) return;
            Observations.Enqueue(value);
            while (Observations.Count > 128) Observations.Dequeue();
        }

        internal static void EnsureParameter(UnityAnimatorWrapper wrapper, int index)
        {
            var context = Current;
            if (!ScopeReady || context == null || wrapper == null) return;
            try
            {
            var cache = wrapper._parametersCache;
            bool missing = cache == null || index >= cache.Length || (index >= 0 && cache[index] == null);
            if (!missing) return;

            var animator = wrapper._animator;
            // Preserve a genuine missing animator / invalid index failure. Never
            // invent a hash or change gameplay animators outside CreateWeapon.
            if (animator == null || index < 0 || index >= animator.parameterCount) return;
            int? beforeCount = cache?.Length;
            int beforeNulls = cache == null ? 0 : Enumerable.Range(0, cache.Length).Count(i => cache[i] == null);
            Record(new { kind = "before", frame = Time.frameCount, index, weapon = context.Weapon?.TemplateId.ToString(),
                loaderPointer = context.Loader?.Pointer.ToString("X"), prefabId = context.Loader?._weaponPrefab?.GetInstanceID(),
                wrapperPointer = wrapper.Pointer.ToString("X"), animatorId = animator.GetInstanceID(),
                animatorName = animator.name, controller = animator.runtimeAnimatorController?.name,
                animatorActiveSelf = animator.gameObject.activeSelf, animatorActive = animator.gameObject.activeInHierarchy,
                context.AnimateWeapon, context.Layer, beforeCount, beforeNulls });
            wrapper.CacheParameters();
            var repaired = wrapper._parametersCache;
            bool inactiveFallback = false;
            // Native CacheParameters returns early for an inactive preview object,
            // although GetParameterNameHash immediately indexes the missing cache.
            // Mirror its verified array/hash-set construction from this animator's
            // real parameters, without activating the model or bypassing lookup.
            if (repaired == null && !animator.gameObject.activeInHierarchy)
            {
                var parameters = animator.parameters;
                if (parameters != null && index < parameters.Length
                    && Enumerable.Range(0, parameters.Length).All(i => parameters[i] != null))
                {
                    var hashes = wrapper._parametersHashes ?? new Il2CppSystem.Collections.Generic.HashSet<int>();
                    hashes.Clear();
                    foreach (var parameter in parameters) hashes.Add(Animator.StringToHash(parameter.name));
                    wrapper._parametersHashes = hashes;
                    wrapper._parametersCache = parameters;
                    repaired = wrapper._parametersCache;
                    inactiveFallback = true;
                }
            }
            bool valid = repaired != null && index < repaired.Length && repaired[index] != null;
            if (pitTeam.Compat.Spt5RuntimeInstallProbe.Enabled)
            {
                var actual = animator.parameters;
                var prefab = context.Loader?._weaponPrefab;
                Record(new { kind = "after", frame = Time.frameCount, index, weapon = context.Weapon?.TemplateId.ToString(),
                    loaderPointer = context.Loader?.Pointer.ToString("X"), prefabId = prefab?.GetInstanceID(),
                    wrapperPointer = wrapper.Pointer.ToString("X"), animatorId = animator.GetInstanceID(),
                    animatorName = animator.name, controller = animator.runtimeAnimatorController?.name,
                    context.AnimateWeapon, context.Layer, beforeCount, beforeNulls,
                    inactiveFallback, animatorActive = animator.gameObject.activeInHierarchy,
                    afterCount = repaired?.Length, repaired = valid,
                    cachedHash = valid ? (int?)repaired[index].nameHash : null,
                    actualHash = actual != null && index < actual.Length ? (int?)actual[index]?.nameHash : null,
                    hashMatches = valid && actual != null && index < actual.Length && actual[index] != null && repaired[index].nameHash == actual[index].nameHash });
            }
            if (!valid)
                pitFireTeam.Log.LogError($"[UI] Weapon preview parameter cache remains invalid: weapon={context.Weapon?.TemplateId}, wrapper={wrapper.Pointer:X}, index={index}, before={beforeCount}, after={repaired?.Length}.");
            }
            catch (Exception ex)
            {
                Record(new { kind = "repair-error", index, wrapperPointer = wrapper.Pointer.ToString("X"), error = ex.ToString() });
                pitFireTeam.Log.LogError($"[UI] Failed to initialize preview animator cache; native parameter lookup remains active: {ex}");
            }
        }

        internal static object Snapshot() => new { scopeReady = ScopeReady, scopeActive = Current != null, observations = Observations.ToArray() };
    }

    internal class UiWeaponPreviewContextPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(PlayerModelLoader), nameof(PlayerModelLoader.CreateWeapon),
            new[] { typeof(float), typeof(bool), typeof(Item), typeof(int) });

        [PatchPrefix]
        private static void Prefix(PlayerModelLoader __instance, bool animateWeapon, Item weapon, int layer, out UiWeaponPreviewCache.Context __state)
        {
            __state = UiWeaponPreviewCache.Current;
            if (!UiWeaponPreviewCache.ScopeReady) return;
            UiWeaponPreviewCache.Current = new UiWeaponPreviewCache.Context { Loader = __instance, Weapon = weapon, AnimateWeapon = animateWeapon, Layer = layer };
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, UiWeaponPreviewCache.Context __state)
        {
            UiWeaponPreviewCache.Current = __state;
            return __exception;
        }
    }

    internal class UiWeaponPreviewParameterCachePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(UnityAnimatorWrapper), nameof(UnityAnimatorWrapper.GetParameterNameHash), new[] { typeof(int) });

        [PatchPrefix]
        private static void Prefix(UnityAnimatorWrapper __instance, int parameterIndex) => UiWeaponPreviewCache.EnsureParameter(__instance, parameterIndex);
    }
}
