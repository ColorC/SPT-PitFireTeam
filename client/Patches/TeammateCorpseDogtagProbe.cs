using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using EFT.InventoryLogic;

namespace pitTeam.Patches
{
    // Opt-in bounded evidence for the native/managed hook boundary.
    internal static class TeammateCorpseDogtagProbe
    {
        private static int _calls;
        private static string? _file;
        private static bool _failed;
        private static readonly object Sync = new();
        internal sealed class NativeTargetAuditResult
        {
            public string status { get; set; } = "error";
            public string declaringType { get; set; } = "";
            public string method { get; set; } = "";
            public string returnType { get; set; } = "";
            public string[] parameterNames { get; set; } = Array.Empty<string>();
            public string[] parameterTypes { get; set; } = Array.Empty<string>();
            public int arity { get; set; }
            public bool isStatic { get; set; }
            public string methodInfoField { get; set; } = "";
            public string? methodInfo { get; set; }
            public string? methodPointer { get; set; }
            public string? nativeClass { get; set; }
            public uint enumeratedMethods { get; set; }
            public string? first16Bytes { get; set; }
            public object? module { get; set; }
            public object[] aliases { get; set; } = Array.Empty<object>();
            public string[] errors { get; set; } = Array.Empty<string>();
            public NativeInstanceFieldAuditResult? nativeInstanceFields { get; set; }
        }

        internal sealed class NativeInstanceFieldAuditResult
        {
            public string status { get; set; } = "error";
            public string closedType { get; set; } = "";
            public int classesVisited { get; set; }
            public int instanceFieldCount { get; set; }
            public int staticFieldCount { get; set; }
            public object[] fields { get; set; } = Array.Empty<object>();
            public object[] requestedOffsetMatches { get; set; } = Array.Empty<object>();
            public string[] errors { get; set; } = Array.Empty<string>();
            public NativeInstanceFieldAuditResult? nativeInstanceFields { get; set; }
        }

        private sealed class NativeInstanceFieldEntry
        {
            public string declaringNativeClass { get; set; } = "";
            public string name { get; set; } = "";
            public string type { get; set; } = "";
            public int flags { get; set; }
            public string nativeOffset { get; set; } = "";
        }

        internal static NativeInstanceFieldAuditResult AuditNativeInstanceFields(Type closedType)
        {
            var result = new NativeInstanceFieldAuditResult { closedType = closedType.FullName ?? closedType.Name };
            var errors = new List<string>();
            var entries = new List<NativeInstanceFieldEntry>();
            var matchedOffsets = new uint[] { 0x88, 0x98, 0x128, 0x140 };
            try
            {
                if (!closedType.IsGenericType
                    || closedType.GetGenericTypeDefinition().FullName != "EFT.BaseLocalGame`1"
                    || closedType.GetGenericArguments().Length != 1
                    || closedType.GetGenericArguments()[0].FullName != "EFT.EftGamePlayerOwner")
                {
                    throw new InvalidOperationException("Native field audit requires the exact closed type EFT.BaseLocalGame<EFT.EftGamePlayerOwner>.");
                }

                IntPtr nativeClass = Il2CppClassPointerStore.GetNativeClassPointer(closedType);
                if (nativeClass == IntPtr.Zero)
                    throw new InvalidOperationException("Native class pointer is zero for " + result.closedType);

                int totalFields = 0;
                int depth = 0;
                while (nativeClass != IntPtr.Zero)
                {
                    if (++depth > 32)
                    {
                        errors.Add("Native parent traversal exceeded 32 classes.");
                        break;
                    }

                    string className = IL2CPP.il2cpp_class_get_name_(nativeClass) ?? "<unknown>";
                    string classNamespace = IL2CPP.il2cpp_class_get_namespace_(nativeClass) ?? "";
                    string declaringType = string.IsNullOrEmpty(classNamespace) ? className : classNamespace + "." + className;
                    result.classesVisited = depth;

                    IntPtr iterator = IntPtr.Zero;
                    IntPtr nativeField;
                    while ((nativeField = IL2CPP.il2cpp_class_get_fields(nativeClass, ref iterator)) != IntPtr.Zero)
                    {
                        if (++totalFields > 4096)
                        {
                            errors.Add("Native field enumeration exceeded 4096 entries.");
                            break;
                        }

                        try
                        {
                            int flags = IL2CPP.il2cpp_field_get_flags(nativeField);
                            if ((((FieldAttributes)flags) & FieldAttributes.Static) != 0)
                            {
                                result.staticFieldCount++;
                                continue;
                            }

                            uint offset = IL2CPP.il2cpp_field_get_offset(nativeField);
                            string fieldName = IL2CPP.il2cpp_field_get_name_(nativeField) ?? "<unknown>";
                            IntPtr fieldType = IL2CPP.il2cpp_field_get_type(nativeField);
                            if (fieldType == IntPtr.Zero)
                                throw new InvalidOperationException("Native field type pointer is zero for " + declaringType + "::" + fieldName);
                            string typeName = IL2CPP.il2cpp_type_get_name_(fieldType) ?? "<unknown>";
                            entries.Add(new NativeInstanceFieldEntry
                            {
                                declaringNativeClass = declaringType,
                                name = fieldName,
                                type = typeName,
                                flags = flags,
                                nativeOffset = "0x" + offset.ToString("X")
                            });
                        }
                        catch (Exception e)
                        {
                            if (errors.Count < 32)
                                errors.Add("field[" + totalFields + "] " + e.GetType().Name + ": " + e.Message);
                        }
                    }

                    if (totalFields > 4096)
                        break;
                    nativeClass = IL2CPP.il2cpp_class_get_parent(nativeClass);
                }

                result.instanceFieldCount = entries.Count;
                result.fields = entries.Cast<object>().ToArray();
                result.requestedOffsetMatches = matchedOffsets.Select(offset => new
                {
                    offset = "0x" + offset.ToString("X"),
                    matches = entries.Where(entry => entry.nativeOffset == "0x" + offset.ToString("X")).ToArray()
                }).Cast<object>().ToArray();
                result.status = errors.Count == 0 ? "ok" : (entries.Count > 0 ? "partial" : "error");
            }
            catch (Exception e)
            {
                if (errors.Count < 32)
                    errors.Add(e.GetType().FullName + ": " + e.Message);
                result.instanceFieldCount = entries.Count;
                result.fields = entries.Cast<object>().ToArray();
                result.status = entries.Count > 0 ? "partial" : "error";
            }

            result.errors = errors.ToArray();
            return result;
        }

        internal static NativeTargetAuditResult AuditGeneratedTarget(Type type, string fieldName, string methodName,
            string returnType, string[] parameterNames, string[] parameterTypes, bool isStatic)
        {
            var r = new NativeTargetAuditResult { declaringType=type.FullName ?? type.Name, method=methodName,
                returnType=returnType, parameterNames=parameterNames, parameterTypes=parameterTypes,
                arity=parameterTypes.Length, isStatic=isStatic, methodInfoField=fieldName };
            var errors = new List<string>(); var aliases = new List<object>();
            try
            {
                if (parameterNames.Length != parameterTypes.Length) throw new InvalidOperationException("Parameter name/type arity mismatch.");
                var f = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (f == null || f.FieldType != typeof(IntPtr) || !f.IsStatic) throw new InvalidOperationException("Generated metadata field missing or invalid: " + fieldName);
                IntPtr mi = (IntPtr)(f.GetValue(null) ?? IntPtr.Zero);
                if (mi == IntPtr.Zero) throw new InvalidOperationException("MethodInfo pointer is zero: " + fieldName);
                r.methodInfo = "0x" + mi.ToInt64().ToString("X");
                IntPtr mp; unsafe { mp = UnityVersionHandler.Wrap((Il2CppMethodInfo*)mi).MethodPointer; }
                if (mp == IntPtr.Zero) throw new InvalidOperationException("MethodPointer is zero: " + fieldName);
                r.methodPointer = "0x" + mp.ToInt64().ToString("X");
                IntPtr cls = Il2CppClassPointerStore.GetNativeClassPointer(type);
                if (cls == IntPtr.Zero) throw new InvalidOperationException("Native class pointer is zero: " + type.FullName);
                r.nativeClass = "0x" + cls.ToInt64().ToString("X");
                bool selectedSeen=false; uint count=0; IntPtr cursor=IntPtr.Zero, candidate;
                while ((candidate=IL2CPP.il2cpp_class_get_methods(cls,ref cursor)) != IntPtr.Zero)
                {
                    if (++count > 4096) { errors.Add("declaring-class method enumeration exceeded 4096"); break; }
                    try {
                        IntPtr cp; unsafe { cp=UnityVersionHandler.Wrap((Il2CppMethodInfo*)candidate).MethodPointer; }
                        if (cp != mp) continue;
                        string aliasName=IL2CPP.il2cpp_method_get_name_(candidate) ?? "<null>"; uint aliasArity=IL2CPP.il2cpp_method_get_param_count(candidate); bool selected=candidate==mi; if(selected){selectedSeen=true;if(aliasName!=methodName || aliasArity!=(uint)parameterTypes.Length) errors.Add("selected MethodInfo alias name/arity mismatch");} aliases.Add(new { methodInfo="0x"+candidate.ToInt64().ToString("X"), name=aliasName, parameterCount=aliasArity, isSelected=selected });
                    } catch(Exception e) { if(errors.Count<16) errors.Add(e.GetType().Name+": "+e.Message); }
                }
                if(!selectedSeen) errors.Add("selected MethodInfo was not found in declaring-class enumeration"); r.enumeratedMethods=count; r.aliases=aliases.ToArray();
                using(Process p=Process.GetCurrentProcess()) {
                    ulong address=unchecked((ulong)mp.ToInt64());
                    foreach(ProcessModule m in p.Modules) {
                        ulong start=unchecked((ulong)m.BaseAddress.ToInt64()), end=start+(uint)m.ModuleMemorySize;
                        if(address<start || address+16>end) continue;
                        byte[] bytes=new byte[16]; Marshal.Copy(mp,bytes,0,16);
                        r.first16Bytes=BitConverter.ToString(bytes).Replace("-"," ");
                        r.module=new { moduleName=m.ModuleName,modulePath=m.FileName,baseAddress="0x"+start.ToString("X"),size=m.ModuleMemorySize,endExclusive="0x"+end.ToString("X") };
                        break;
                    }
                }
                if(r.module==null) errors.Add("method pointer is outside loaded modules or fewer than 16 bytes remain");
                r.status=errors.Count==0?"ok":"partial";
            } catch(Exception e) { if(errors.Count<16) errors.Add(e.GetType().FullName+": "+e.Message); r.status="error"; }
            r.errors=errors.ToArray(); return r;
        }

        public static int Begin(ItemController controller, Item item)
        {
            string? root = Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT");
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root) || !Directory.Exists(root)) return 0;
            int id = Interlocked.Increment(ref _calls);
            if (id > 24) return 0;
            _file ??= Path.Combine(Path.GetFullPath(root), "probe-dogtag-boundary.log");
            Trace(id, "prefix.enter", "owner=" + (controller?.Pointer.ToString("X") ?? "null") +
                " item=" + (item?.Pointer.ToString("X") ?? "null"));
            return id;
        }

        public static void AuditNativeTarget()
        {
            string? root = Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT");
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root) || !Directory.Exists(root)) return;
            string outputPath = Path.Combine(Path.GetFullPath(root), "native-target-dogtag.json");
            object report;
            try
            {
                const string fieldName = "NativeMethodInfoPtr_CanMoveDogtag_Public_Boolean_Item_0";
                FieldInfo? methodInfoField = typeof(ItemController).GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (methodInfoField == null || methodInfoField.FieldType != typeof(IntPtr) || !methodInfoField.IsStatic)
                    throw new InvalidOperationException("Generated target MethodInfo field missing or has unexpected shape: " + fieldName);
                IntPtr targetMethodInfo = (IntPtr)(methodInfoField.GetValue(null) ?? IntPtr.Zero);
                if (targetMethodInfo == IntPtr.Zero)
                    throw new InvalidOperationException("Generated target MethodInfo pointer is zero.");
                INativeMethodInfoStruct WrapMethodInfo(IntPtr nativeMethodInfo)
                {
                    unsafe
                    {
                        return UnityVersionHandler.Wrap((Il2CppMethodInfo*)nativeMethodInfo);
                    }
                }
                object[] operationTargets = new[]
                {
                    AuditGeneratedTarget(typeof(ItemManipulator), "NativeMethodInfoPtr_Move_Public_Static_OperationResult_1_MoveResult_Item_ItemAddress_ItemController_Boolean_0", "Move", "Diz.LanguageExtensions.OperationResult`1<EFT.InventoryLogic.MoveResult>", new[] { "item", "to", "itemController", "simulate" }, new[] { "EFT.InventoryLogic.Item", "EFT.InventoryLogic.ItemAddress", "EFT.InventoryLogic.ItemController", "System.Boolean" }, true),
                    AuditGeneratedTarget(typeof(ItemManipulator), "NativeMethodInfoPtr_Remove_Public_Static_OperationResult_1_RemoveResult_Item_ItemController_Boolean_0", "Remove", "Diz.LanguageExtensions.OperationResult`1<EFT.InventoryLogic.RemoveResult>", new[] { "item", "itemController", "simulate" }, new[] { "EFT.InventoryLogic.Item", "EFT.InventoryLogic.ItemController", "System.Boolean" }, true),
                    AuditGeneratedTarget(typeof(InventoryController), "NativeMethodInfoPtr_Examined_Public_Virtual_Boolean_Item_0", "Examined", "System.Boolean", new[] { "item" }, new[] { "EFT.InventoryLogic.Item" }, false)
                };
                IntPtr targetMethodPointer = WrapMethodInfo(targetMethodInfo).MethodPointer;
                if (targetMethodPointer == IntPtr.Zero)
                    throw new InvalidOperationException("Wrapped target MethodPointer is zero.");
                IntPtr classPointer = Il2CppClassPointerStore.GetNativeClassPointer(typeof(ItemController));
                if (classPointer == IntPtr.Zero)
                    throw new InvalidOperationException("ItemController native class pointer is zero.");
                var aliases = new List<object>();
                var enumerationErrors = new List<string>();
                uint enumerated = 0;
                IntPtr iterator = IntPtr.Zero;
                IntPtr nativeMethodInfo;
                while ((nativeMethodInfo = IL2CPP.il2cpp_class_get_methods(classPointer, ref iterator)) != IntPtr.Zero)
                {
                    if (++enumerated > 4096)
                    {
                        enumerationErrors.Add("ItemController method enumeration exceeded 4096 entries.");
                        break;
                    }
                    try
                    {
                        IntPtr methodPointer = WrapMethodInfo(nativeMethodInfo).MethodPointer;
                        if (methodPointer != targetMethodPointer) continue;
                        string methodName = IL2CPP.il2cpp_method_get_name_(nativeMethodInfo) ?? "<null>";
                        uint parameterCount = IL2CPP.il2cpp_method_get_param_count(nativeMethodInfo);
                        aliases.Add(new
                        {
                            methodInfoNativePointer = "0x" + nativeMethodInfo.ToInt64().ToString("X"),
                            methodName,
                            parameterCount,
                            methodPointer = "0x" + methodPointer.ToInt64().ToString("X"),
                            isSelectedTarget = nativeMethodInfo == targetMethodInfo
                        });
                    }
                    catch (Exception ex)
                    {
                        if (enumerationErrors.Count < 32)
                            enumerationErrors.Add("methodInfo=0x" + nativeMethodInfo.ToInt64().ToString("X") + ": " + ex.GetType().FullName + ": " + ex.Message);
                    }
                }
                object? moduleRecord = null;
                string? first16Bytes = null;
                using (Process process = Process.GetCurrentProcess())
                {
                    foreach (ProcessModule module in process.Modules)
                    {
                        ulong moduleBase = unchecked((ulong)module.BaseAddress.ToInt64());
                        ulong moduleEnd = moduleBase + (uint)module.ModuleMemorySize;
                        ulong targetAddress = unchecked((ulong)targetMethodPointer.ToInt64());
                        if (targetAddress < moduleBase || targetAddress + 16 > moduleEnd) continue;
                        byte[] bytes = new byte[16];
                        Marshal.Copy(targetMethodPointer, bytes, 0, bytes.Length);
                        first16Bytes = BitConverter.ToString(bytes).Replace("-", " ");
                        moduleRecord = new
                        {
                            moduleName = module.ModuleName,
                            modulePath = module.FileName,
                            baseAddress = "0x" + moduleBase.ToString("X"),
                            size = module.ModuleMemorySize,
                            endExclusive = "0x" + moduleEnd.ToString("X")
                        };
                        break;
                    }
                }
                report = new
                {
                    status = moduleRecord == null ? "target-outside-loaded-module" : enumerationErrors.Count == 0 ? "ok" : "partial",
                    target = new
                    {
                        declaringType = "EFT.InventoryLogic.ItemController",
                        method = "CanMoveDogtag",
                        returnType = "System.Boolean",
                        parameterTypes = new[] { "EFT.InventoryLogic.Item" },
                        methodInfoField = fieldName,
                        methodInfoNativePointer = "0x" + targetMethodInfo.ToInt64().ToString("X"),
                        wrapperMethodPointer = "0x" + targetMethodPointer.ToInt64().ToString("X"),
                        first16Bytes,
                        module = moduleRecord
                    },
                    enumeration = new { nativeClassPointer = "0x" + classPointer.ToInt64().ToString("X"), methodCount = enumerated, scope = "EFT.InventoryLogic.ItemController only", aliases = aliases.ToArray(), errors = enumerationErrors.ToArray() },
                    operationTargets,
                    evidence = new { noPatchBehaviorChanged = true, auditRunsBeforeMovePatchEnable = true, nativeAliasesAreSameClassOnly = true }
                };
            }
            catch (Exception ex)
            {
                report = new
                {
                    status = "error",
                    target = new { declaringType = "EFT.InventoryLogic.ItemController", method = "CanMoveDogtag", returnType = "System.Boolean", parameterTypes = new[] { "EFT.InventoryLogic.Item" } },
                    error = ex.GetType().FullName + ": " + ex.Message,
                    evidence = new { noPatchBehaviorChanged = true, auditRunsBeforeMovePatchEnable = true, nativeAliasesAreSameClassOnly = true }
                };
            }
            try
            {
                File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(report,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                // Diagnostic collection must never alter the dogtag guard's installation or decision.
                pitFireTeam.Log?.LogWarning("[Dogtag] Native target audit write failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public static void Trace(int id, string stage, string detail = "")
        {
            if (id <= 0 || _failed || _file == null) return;
            try
            {
                lock (Sync)
                    File.AppendAllText(_file, DateTime.UtcNow.ToString("O") + " call=" + id +
                        " " + stage + " " + detail + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { _failed = true; } // Evidence failures never change the protection decision.
        }
    }
}
