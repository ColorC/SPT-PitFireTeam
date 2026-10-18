using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static partial class Program
{
    static void CheckJsonBoundary(ModuleDefinition mod, Index game, Report r)
    {
        var ct = game.Types.Where(x => x.FullName == "EFT.EftJsonConverters").ToArray();
        bool getter =
            ct.Length == 1
            && ct[0]
                .Type.Methods.Any(m =>
                    m.Name == "get_Converters" && m.IsStatic && m.Parameters.Count == 0
                );
        Add(
            r,
            "json-native-converters-export",
            getter,
            "EFT.EftJsonConverters.Converters getter exports="
                + getter
                + " paths="
                + string.Join(" | ", ct.Select(x => x.Path))
        );
        var calls = All(mod)
            .SelectMany(t => t.Methods)
            .Where(m => m.HasBody)
            .SelectMany(m =>
                m.Body.Instructions.Where(i =>
                        i.Operand is MethodReference x
                        && x.DeclaringType.FullName == "JsonExtensions"
                        && (x.Name == "ToJson" || x.Name == "ParseJsonTo")
                    )
                    .Select(i => (m, (MethodReference)i.Operand))
            )
            .ToArray();
        var targets = game
            .Types.Where(x => x.FullName == "JsonExtensions")
            .SelectMany(x => x.Type.Methods)
            .ToArray();
        foreach (var c in calls)
        {
            Add(
                r,
                "json-extension-export",
                targets.Count(m => Key(m) == Key(c.Item2)) == 1,
                c.Item2.FullName
                    + " exact Assembly-CSharp export; caller="
                    + c.m.DeclaringType.FullName
            );
            if (c.Item2 is GenericInstanceMethod g)
                foreach (var a in g.GenericArguments)
                {
                    bool dto = IsManagedJsonDto(a, mod);
                    Add(
                        r,
                        "json-managed-dto-boundary",
                        !dto,
                        c.Item2.FullName
                            + " T="
                            + a.FullName
                            + " managedDto="
                            + dto
                            + (dto ? "; use SptJsonBoundary." : "")
                    );
                }
        }
        int uses = All(mod)
            .SelectMany(t => t.Methods)
            .Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions)
            .Count(i =>
                i.Operand is MethodReference x
                && x.DeclaringType.FullName == "EFT.EftJsonConverters"
                && x.Name == "get_Converters"
            );
        Add(
            r,
            "json-native-converter-use",
            getter && (uses > 0 || calls.Length == 0),
            "Exact native converter getter call count=" + uses + "; runtime behavior unverified."
        );
        Add(
            r,
            "json-extension-inventory",
            true,
            "JsonExtensions calls=" + calls.Length + "; dynamic calls unresolved."
        );
    }

    static bool IsManagedJsonDto(TypeReference t, ModuleDefinition mod)
    {
        if (t is GenericInstanceType g)
            return g.GenericArguments.Any(a => IsManagedJsonDto(a, mod));
        if (t is ArrayType a)
            return IsManagedJsonDto(a.ElementType, mod);
        var x = All(mod).FirstOrDefault(y => y.FullName == t.FullName);
        return x != null
            && x.BaseType?.FullName != "Il2CppSystem.Object"
            && x.BaseType?.FullName != "Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"
            && x.BaseType?.FullName != "Il2CppSystem.ValueType";
    }

    static void CheckConfigBindTypeIdentity(ModuleDefinition mod, Index runtime, Report r)
    {
        var calls = All(mod)
            .SelectMany(t => t.Methods)
            .Where(m => m.HasBody)
            .SelectMany(m =>
                m.Body.Instructions.Where(i =>
                        i.Operand is MethodReference method && IsConfigFileBind(method)
                    )
                    .Select(i => (Caller: m, Method: (MethodReference)i.Operand))
            )
            .ToArray();
        foreach (var call in calls)
        {
            if (!TryGetConfigBindArgument(call.Method, out var argument))
            {
                Add(
                    r,
                    "config-bind-type-identity",
                    false,
                    call.Method.FullName + " is not a closed ConfigFile.Bind<T> call."
                );
                continue;
            }
            var localDefinitions = All(mod)
                .Where(local => local.FullName == argument.FullName)
                .ToArray();
            var runtimeMatches = runtime
                .Types.Where(entry =>
                    entry.FullName == argument.FullName && entry.Assembly != mod.Assembly.Name.Name
                )
                .ToArray();
            var conflicts = ConfigTypeConflicts(argument, mod, runtime).ToArray();
            var scope = argument.Scope is AssemblyNameReference assembly
                ? assembly.FullName
                : argument.Scope?.Name ?? "<local>";
            Add(
                r,
                "config-bind-type-identity",
                conflicts.Length == 0,
                call.Method.FullName
                    + " T="
                    + argument.FullName
                    + " scope="
                    + scope
                    + " localTypeDefs="
                    + localDefinitions.Length
                    + " runtimeSameNameExports="
                    + runtimeMatches.Length
                    + " conflictingRuntimeExports="
                    + string.Join(" | ", conflicts.Select(x => x.Assembly + "@" + x.Path))
                    + " caller="
                    + call.Caller.FullName
            );
        }
        if (calls.Length == 0)
            Add(
                r,
                "config-bind-type-identity",
                true,
                "No ConfigFile.Bind<T> method operands; no local/runtime identity conflict observed."
            );
    }

    static bool IsConfigFileBind(MethodReference method) =>
        method.Name == "Bind"
        && method.DeclaringType.FullName == "BepInEx.Configuration.ConfigFile";

    static bool TryGetConfigBindArgument(MethodReference method, out TypeReference argument)
    {
        if (
            IsConfigFileBind(method)
            && method is GenericInstanceMethod generic
            && generic.GenericArguments.Count == 1
        )
        {
            argument = generic.GenericArguments[0];
            return true;
        }
        argument = null!;
        return false;
    }

    static IEnumerable<Entry> ConfigTypeConflicts(
        TypeReference type,
        ModuleDefinition mod,
        Index runtime
    )
    {
        if (type is GenericInstanceType generic)
            foreach (var argument in generic.GenericArguments)
            foreach (var conflict in ConfigTypeConflicts(argument, mod, runtime))
                yield return conflict;
        else if (type is ArrayType array)
            foreach (var conflict in ConfigTypeConflicts(array.ElementType, mod, runtime))
                yield return conflict;

        var exports = runtime
            .Types.Where(entry =>
                entry.FullName == type.FullName
                && entry.Assembly != mod.Assembly.Name.Name
                && entry.Type.Namespace.StartsWith(
                    "BepInEx.Configuration",
                    StringComparison.Ordinal
                )
            )
            .ToArray();
        if (exports.Length == 0)
            yield break;

        bool localDefinition = All(mod).Any(local => local.FullName == type.FullName);
        foreach (var export in exports)
            if (localDefinition || !MatchesConfigTypeScope(type, export))
                yield return export;
    }

    static bool MatchesConfigTypeScope(TypeReference type, Entry export)
    {
        if (type.Scope is not AssemblyNameReference scope)
            return false;
        return scope.Name == export.Assembly
            && scope.Version == export.Version
            && Convert
                .ToHexString(scope.PublicKeyToken ?? Array.Empty<byte>())
                .Equals(export.Token, StringComparison.OrdinalIgnoreCase);
    }

    static (string, bool)[] ConfigBindIdentitySelfTest()
    {
        const string ns = "BepInEx.Configuration";
        using var mod = ModuleDefinition.CreateModule("ConfigBindConflictFixture", ModuleKind.Dll);
        using var runtime = ModuleDefinition.CreateModule(
            "BepInEx.KeyboardShortcut",
            ModuleKind.Dll
        );
        var local = new TypeDefinition(
            ns,
            "KeyboardShortcut",
            TypeAttributes.Public,
            new TypeReference("System", "ValueType", mod, mod.TypeSystem.Object.Scope, false)
        );
        var exported = new TypeDefinition(
            ns,
            "KeyboardShortcut",
            TypeAttributes.Public,
            new TypeReference(
                "System",
                "ValueType",
                runtime,
                runtime.TypeSystem.Object.Scope,
                false
            )
        );
        mod.Types.Add(local);
        runtime.Types.Add(exported);
        var reference = new AssemblyNameReference(
            runtime.Assembly.Name.Name,
            runtime.Assembly.Name.Version
        );
        mod.AssemblyReferences.Add(reference);
        var entry = new Entry(
            runtime.Assembly.Name.Name,
            runtime.Assembly.Name.Version,
            Convert.ToHexString(runtime.Assembly.Name.PublicKeyToken ?? Array.Empty<byte>()),
            "<fixture>",
            exported
        );
        var conflictCall = MakeConfigBindCall(mod, local, reference);
        bool rejected =
            TryGetConfigBindArgument(conflictCall, out var localArg)
            && ConfigTypeConflicts(localArg, mod, new Index { Types = new() { entry } }).Any();
        using var supported = ModuleDefinition.CreateModule(
            "ConfigBindSupportedFixture",
            ModuleKind.Dll
        );
        var supportedRef = new AssemblyNameReference(
            runtime.Assembly.Name.Name,
            runtime.Assembly.Name.Version
        );
        supported.AssemblyReferences.Add(supportedRef);
        var imported = new TypeReference(ns, "KeyboardShortcut", supported, supportedRef, false);
        var supportedCall = MakeConfigBindCall(supported, imported, supportedRef);
        bool accepted =
            TryGetConfigBindArgument(supportedCall, out var supportedArg)
            && !ConfigTypeConflicts(supportedArg, supported, new Index { Types = new() { entry } })
                .Any();
        using var wrongScopeMod = ModuleDefinition.CreateModule(
            "ConfigBindWrongScopeFixture",
            ModuleKind.Dll
        );
        var wrongScope = new AssemblyNameReference(
            "Legacy.KeyboardShortcut",
            runtime.Assembly.Name.Version
        );
        wrongScopeMod.AssemblyReferences.Add(wrongScope);
        var wrongType = new TypeReference(ns, "KeyboardShortcut", wrongScopeMod, wrongScope, false);
        var wrongCall = MakeConfigBindCall(wrongScopeMod, wrongType, wrongScope);
        bool rejectedWrongScope =
            TryGetConfigBindArgument(wrongCall, out var wrongArg)
            && ConfigTypeConflicts(wrongArg, wrongScopeMod, new Index { Types = new() { entry } })
                .Any();
        return new[]
        {
            (
                "ConfigFile.Bind<T> rejects local KeyboardShortcut TypeDef shadowing runtime export",
                rejected
            ),
            ("ConfigFile.Bind<T> accepts imported KeyboardShortcut AssemblyScope", accepted),
            ("ConfigFile.Bind<T> rejects a same-name foreign AssemblyScope", rejectedWrongScope),
        };
    }

    static GenericInstanceMethod MakeConfigBindCall(
        ModuleDefinition module,
        TypeReference argument,
        AssemblyNameReference scope
    )
    {
        var config = new TypeReference("BepInEx.Configuration", "ConfigFile", module, scope, false);
        var method = new MethodReference("Bind", module.TypeSystem.Void, config);
        method.GenericParameters.Add(new GenericParameter("T", method));
        var call = new GenericInstanceMethod(method);
        call.GenericArguments.Add(argument);
        return call;
    }
}
