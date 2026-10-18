using HarmonyLib;
using SPTushonka.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace pitTeam.Patches
{
    internal static class ModulePatchFinalizerRegistration
    {
        internal static void Register(ModulePatch patch)
        {
            if (patch == null) return;
            Type type = patch.GetType();
            try
            {
                MethodBase target = type.GetMethod("GetTargetMethod", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(patch, null) as MethodBase;
                MethodInfo prefix = FindAttributedMethod(type, typeof(PatchPrefixAttribute));
                MethodInfo postfix = FindAttributedMethod(type, typeof(PatchPostfixAttribute));
                MethodInfo finalizer = FindAttributedMethod(type, typeof(HarmonyFinalizer));
                if (target == null || finalizer == null) throw new InvalidOperationException("Target or HarmonyFinalizer missing for " + type.FullName);
                bool finalizerOnly = prefix == null && postfix == null;
                HarmonyLib.Patches before = Harmony.GetPatchInfo(target);
                Harmony ownedHarmony = null;
                string owner;
                if (finalizerOnly)
                {
                    ownedHarmony = ReadOwnedHarmonyInstance(patch);
                    owner = ownedHarmony.Id;
                    if (String.IsNullOrEmpty(owner)) throw new InvalidOperationException("Live ModulePatch Harmony.Id is empty for " + type.FullName);
                }
                else
                {
                    if (before == null) throw new InvalidOperationException("GetPatchInfo returned null for a patch declaring prefix/postfix; target=" + DescribeMethod(target));
                    string prefixOwner = prefix == null ? null : FindUniqueOwner(before.Prefixes, prefix, "prefix", target);
                    string postfixOwner = postfix == null ? null : FindUniqueOwner(before.Postfixes, postfix, "postfix", target);
                    if (prefixOwner != null && postfixOwner != null && prefixOwner != postfixOwner) throw new InvalidOperationException("Runtime prefix/postfix owners differ; prefix=" + prefixOwner + "; postfix=" + postfixOwner + "; target=" + DescribeMethod(target));
                    owner = prefixOwner ?? postfixOwner;
                    if (String.IsNullOrEmpty(owner)) throw new InvalidOperationException("No unique runtime prefix/postfix owner for " + type.FullName);
                }
                int beforePrefixCount = CountOwned(before?.Prefixes, owner, prefix);
                int beforePostfixCount = CountOwned(before?.Postfixes, owner, postfix);
                int beforeFinalizerCount = CountOwned(before?.Finalizers, owner, finalizer);
                if (beforeFinalizerCount > 1) throw new InvalidOperationException("Duplicate owner/method finalizers already exist for " + type.FullName + "; count=" + beforeFinalizerCount);
                string prefixInventory = DescribePatches(before?.Prefixes);
                string postfixInventory = DescribePatches(before?.Postfixes);
                if (beforeFinalizerCount == 0)
                {
                    Harmony registrar = ownedHarmony ?? new Harmony(owner);
                    if (registrar.Id != owner) throw new InvalidOperationException("Registrar Harmony.Id does not match runtime owner for " + type.FullName);
                    registrar.Patch(target, null, null, null, new HarmonyMethod(finalizer), null);
                }
                HarmonyLib.Patches after = Harmony.GetPatchInfo(target);
                int afterPrefixCount = CountOwned(after?.Prefixes, owner, prefix);
                int afterPostfixCount = CountOwned(after?.Postfixes, owner, postfix);
                int afterFinalizerCount = CountOwned(after?.Finalizers, owner, finalizer);
                bool unchangedHooks = prefixInventory == DescribePatches(after?.Prefixes) && postfixInventory == DescribePatches(after?.Postfixes);
                bool verified = afterFinalizerCount == 1 && beforePrefixCount == afterPrefixCount && beforePostfixCount == afterPostfixCount && unchangedHooks;
                if (verified) global::pitTeam.pitFireTeam.Log.LogInfo("[Harmony] finalizer verified target=" + DescribeMethod(target) + " owner=" + owner + " finalizer=" + DescribeMethod(finalizer) + " prefixCount=" + afterPrefixCount + " postfixCount=" + afterPostfixCount + " GetPatchInfoBeforeNull=" + (before == null) + " GetPatchInfoAfterNull=" + (after == null));
                else global::pitTeam.pitFireTeam.Log.LogError("[Harmony] finalizer verification failed target=" + DescribeMethod(target) + " owner=" + owner + " GetPatchInfoBeforeNull=" + (before == null) + " GetPatchInfoAfterNull=" + (after == null) + " prefixCount=" + beforePrefixCount + "->" + afterPrefixCount + " postfixCount=" + beforePostfixCount + "->" + afterPostfixCount + " finalizerCount=" + afterFinalizerCount + " hooksUnchanged=" + unchangedHooks);
            }
            catch (Exception ex)
            {
                global::pitTeam.pitFireTeam.Log.LogError("[Harmony] finalizer registration failed for " + type.FullName);
                global::pitTeam.pitFireTeam.Log.LogError(ex);
            }
        }
        private static Harmony ReadOwnedHarmonyInstance(ModulePatch patch)
        {
            FieldInfo found = null;
            for (Type current = typeof(ModulePatch); current != null; current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType != typeof(Harmony)) continue;
                    if (found != null) throw new InvalidOperationException("Multiple Harmony instance fields found in ModulePatch base hierarchy");
                    found = field;
                }
            }
            if (found == null) throw new InvalidOperationException("No Harmony-typed instance field found in ModulePatch base hierarchy");
            Harmony value = found.GetValue(patch) as Harmony;
            if (value == null) throw new InvalidOperationException("ModulePatch Harmony instance field is null: " + found.DeclaringType?.FullName + "." + found.Name);
            return value;
        }
        private static MethodInfo FindAttributedMethod(Type type, Type attributeType)
        {
            MethodInfo found = null;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttributes(attributeType, false).Length == 0) continue;
                if (found != null) throw new InvalidOperationException("Multiple " + attributeType.Name + " methods on " + type.FullName);
                found = method;
            }
            return found;
        }
        private static string FindUniqueOwner(IEnumerable<HarmonyLib.Patch> patches, MethodInfo method, string kind, MethodBase target)
        {
            if (patches == null) throw new InvalidOperationException("GetPatchInfo " + kind + " list is null; target=" + DescribeMethod(target));
            HarmonyLib.Patch found = null;
            int count = 0;
            foreach (HarmonyLib.Patch item in patches) if (SameMethod(item.PatchMethod, method)) { found = item; count++; }
            if (count != 1 || String.IsNullOrEmpty(found?.owner)) throw new InvalidOperationException("Expected one runtime " + kind + " matching " + DescribeMethod(method) + "; count=" + count + "; patches=" + DescribePatches(patches));
            return found.owner;
        }
        private static bool SameMethod(MethodInfo left, MethodInfo right)
        {
            if (left == null || right == null) return false;
            if (left.Equals(right)) return true;
            if (left.DeclaringType != right.DeclaringType || left.Name != right.Name || left.ReturnType != right.ReturnType || left.IsStatic != right.IsStatic) return false;
            ParameterInfo[] a = left.GetParameters(); ParameterInfo[] b = right.GetParameters();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i].ParameterType != b[i].ParameterType) return false;
            return true;
        }
        private static string DescribeMethod(MethodBase method)
        {
            if (method == null) return "<null>";
            StringBuilder text = new StringBuilder(); text.Append(method.DeclaringType?.FullName).Append(".").Append(method.Name).Append("(");
            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++) { if (i > 0) text.Append(","); text.Append(parameters[i].ParameterType.FullName); }
            return text.Append(")").ToString();
        }
        private static string DescribePatches(IEnumerable<HarmonyLib.Patch> patches)
        {
            if (patches == null) return "[]";
            List<string> items = new List<string>();
            foreach (HarmonyLib.Patch item in patches) items.Add(item.owner + ":" + DescribeMethod(item.PatchMethod));
            items.Sort(StringComparer.Ordinal);
            return "[" + String.Join(";", items) + "]";
        }
        private static int CountOwned(IEnumerable<HarmonyLib.Patch> patches, string owner, MethodInfo method)
        {
            if (patches == null || method == null) return 0;
            int count = 0;
            foreach (HarmonyLib.Patch item in patches) if (item.owner == owner && SameMethod(item.PatchMethod, method)) count++;
            return count;
        }
    }
}
