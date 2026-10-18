using System.Text.Json;

internal static partial class Program
{
    static int NativeTargetAudit(Args args)
    {
        string input = Path.GetFullPath(args.R("input"));
        if (!System.IO.File.Exists(input))
            throw new Usage("Native target evidence was not provided: " + input);
        string output = Out(args.G("report"), "offline-native-target-audit.json");
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(input));
        var report = AnalyzeNativeTarget(doc.RootElement, input, args.G("runtime-source") ?? "", args.G("metadata"));
        Write(output, report);
        Console.WriteLine(JsonSerializer.Serialize(report, J));
        Console.WriteLine("Report: " + output);
        return 0;
    }

    static int NativeTargetSelfTest(Args args)
    {
        string fixture = Path.GetFullPath(args.G("fixtures") ??
            Path.Combine(AppContext.BaseDirectory, "fixtures", "native-target-audit-cases.json"));
        if (!System.IO.File.Exists(fixture))
            throw new Usage("Native target fixtures were not provided: " + fixture);
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(fixture));
        var results = new List<object>();
        bool passed = true;
        foreach (var f in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var actual = AnalyzeNativeTarget(f.GetProperty("report"), f.GetProperty("name").GetString()!, "", null);
            string expected = f.GetProperty("expectedClassification").GetString()!;
            int count = f.GetProperty("expectedConflictCount").GetInt32();
            bool ok = actual.Classification == expected && actual.ConflictCount == count;
            passed &= ok;
            results.Add(new { name = f.GetProperty("name").GetString(), expected,
                actual = actual.Classification, actual.ConflictCount, passed = ok });
        }
        var result = new { command = "native-target-self-test", fixture,
            passed, caseCount = results.Count, cases = results };
        if (args.G("report") is { Length: > 0 } reportPath)
            Write(Out(reportPath, "offline-native-target-self-test.json"), result);
        Console.WriteLine(JsonSerializer.Serialize(result, J));
        return passed ? 0 : 1;
    }

    static NativeTargetResult AnalyzeNativeTarget(JsonElement root, string input, string runtimeSource, string? metadataPath)
    {
        var target = Get(root, "target");
        var enumeration = Get(root, "enumeration");
        string status = Text(root, "status"), scope = Text(enumeration, "scope");
        string type = Text(target, "declaringType"), method = Text(target, "method");
        string pointer = Text(target, "wrapperMethodPointer"), info = Text(target, "methodInfoNativePointer");
        int arity = Count(Get(target, "parameterTypes"));
        var aliasNode = Get(enumeration, "aliases");
        var errorNode = Get(enumeration, "errors");
        var aliases = aliasNode.ValueKind == JsonValueKind.Array
            ? aliasNode.EnumerateArray().Select(x => new NativeAlias(Text(x, "methodName"),
                Text(x, "methodPointer"), Text(x, "methodInfoNativePointer"),
                Number(x, "parameterCount"))).ToArray()
            : Array.Empty<NativeAlias>();
        bool targetFound = aliases.Any(x => x.Info == info ||
            (x.Name == method && x.Pointer == pointer));
        bool partial = status != "ok" || scope.Length == 0 || type.Length == 0 ||
            method.Length == 0 || pointer.Length == 0 || info.Length == 0 ||
            aliasNode.ValueKind != JsonValueKind.Array || errorNode.ValueKind != JsonValueKind.Array ||
            errorNode.GetArrayLength() != 0 || !targetFound;
        var same = aliases.Where(x => x.Pointer == pointer).ToArray();
        var otherSame = same.Where(x => x.Info != info && x.Name != method).ToArray();
        var conflicts = otherSame.Where(x => x.Arity != arity).ToArray();
        string classification = partial ? "partial-evidence"
            : conflicts.Length > 0 ? "shared-incompatible-signatures"
            : otherSame.Length > 0 ? "shared-same-arity-signature-unverified"
            : aliases.Any(x => x.Name != method && x.Pointer != pointer) ? "separate-pointers"
            : "target-only";
        string[] metadataPaths = string.IsNullOrWhiteSpace(metadataPath) ? Array.Empty<string>() : metadataPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var metadataSources = metadataPaths.Select(path =>
        {
            string full = Path.GetFullPath(path);
            using var metadata = JsonDocument.Parse(System.IO.File.ReadAllText(full));
            return (Path: full, Root: metadata.RootElement.Clone());
        }).ToArray();
        var operations = Get(root, "operationTargets");
        object[] observations = operations.ValueKind == JsonValueKind.Array
            ? operations.EnumerateArray().Select(x => (object)new
            {
                declaringType = First(x, "declaringType", "targetType", "typeFullName"),
                method = First(x, "method", "targetMethod", "methodName"),
                returnType = Text(x, "returnType"),
                methodInfoField = Text(x, "methodInfoField"),
                methodInfo = Text(x, "methodInfo"),
                methodPointer = Text(x, "methodPointer"),
                nativeClass = Text(x, "nativeClass"),
                enumeratedMethods = Number(x, "enumeratedMethods"),
                sameDeclaringTypeAsPrimaryTarget = First(x, "declaringType", "targetType", "typeFullName") == type,
                aliasScope = "Aliases enumerated from this operation target declaring class only; not a global uniqueness claim.",
                parameterMetadata = ResolveParameterMetadata(metadataSources, x),
                parameterTypes = Strings(Get(x, "parameterTypes")),
                parameterNameTypeMetadataPresent = Get(x, "parameters").ValueKind == JsonValueKind.Array,
                parameters = ProjectParameters(Get(x, "parameters")),
                aliases = ProjectOperationAliases(Get(x, "aliases")),
                errors = Strings(Get(x, "errors"))
            }).ToArray()
            : Array.Empty<object>();
        return new NativeTargetResult
        {
            Command = "native-target-audit", Input = input,
            Status = partial ? "partial-evidence" : "observed",
            RuntimeObservationSource = runtimeSource,
            MetadataInput = string.Join(";", metadataSources.Select(source => source.Path)),
            Target = new NativeTarget { DeclaringType = type, Method = method,
                ReturnType = Text(target, "returnType"), ParameterTypes = Strings(Get(target, "parameterTypes")),
                MethodInfoPointer = info, MethodPointer = pointer },
            Scope = scope,
            ScopeBoundary = "Only the declaring class enumeration supplied by this report; not a global method-pointer uniqueness claim.",
            Classification = classification, ConflictCount = conflicts.Length,
            SamePointerAliases = same,
            DifferentPointerAliases = aliases.Where(x => x.Name != method && x.Pointer != pointer).ToArray(),
            EnumerationErrors = Strings(errorNode), OperationTargetsObservationOnly = observations
        };
    }

    static JsonElement Get(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var value) ? value : default;
    static string Text(JsonElement e, string key) =>
        Get(e, key).ValueKind == JsonValueKind.String ? Get(e, key).GetString() ?? "" : "";
    static string First(JsonElement e, params string[] keys)
    {
        foreach (string key in keys) { string value = Text(e, key); if (value.Length > 0) return value; }
        return "";
    }
    static int Number(JsonElement e, string key) =>
        Get(e, key).ValueKind == JsonValueKind.Number && Get(e, key).TryGetInt32(out int n) ? n : -1;
    static bool Bool(JsonElement e, string key) => Get(e, key).ValueKind == JsonValueKind.True;
    static int Count(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.GetArrayLength() : -1;
    static string[] Strings(JsonElement e) => e.ValueKind == JsonValueKind.Array
        ? e.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "").ToArray()
        : Array.Empty<string>();
    static object[] ProjectParameters(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray()
                .Select(parameter => (object)new
                {
                    Name = First(parameter, "Name", "name"),
                    Type = First(parameter, "Type", "type"),
                })
                .ToArray()
            : Array.Empty<object>();    static ParameterMetadataProjection ResolveParameterMetadata(
        (string Path, JsonElement Root)[] sources,
        JsonElement operation
    )
    {
        if (sources.Length == 0)
            return new ParameterMetadataProjection { Status = "metadata-not-provided" };
        string type = First(operation, "declaringType", "targetType", "typeFullName");
        string method = First(operation, "method", "targetMethod", "methodName");
        string result = Text(operation, "returnType");
        string[] parameterTypes = Strings(Get(operation, "parameterTypes"));
        var candidates = sources
            .SelectMany(source =>
            {
                var rows = Get(source.Root, "matches");
                if (rows.ValueKind != JsonValueKind.Array)
                    return Enumerable.Empty<(string Path, JsonElement Method)>();
                return rows.EnumerateArray()
                    .Where(row => Text(row, "FullName") == type)
                    .SelectMany(row =>
                    {
                        var methods = Get(row, "Methods");
                        return methods.ValueKind == JsonValueKind.Array
                            ? methods.EnumerateArray().Select(candidate =>
                                (Path: source.Path, Method: candidate))
                            : Enumerable.Empty<(string Path, JsonElement Method)>();
                    });
            })
            .Where(candidate =>
                Text(candidate.Method, "Name") == method
                && Text(candidate.Method, "Return") == result
                && Get(candidate.Method, "Parameters").ValueKind == JsonValueKind.Array
                && Get(candidate.Method, "Parameters")
                    .EnumerateArray()
                    .Select(parameter => Text(parameter, "Type"))
                    .SequenceEqual(parameterTypes)
            )
            .ToArray();
        return new ParameterMetadataProjection
        {
            Status = candidates.Length == 1
                ? "exact-signature"
                : candidates.Length == 0 ? "unresolved" : "ambiguous",
            Source = candidates.Length == 1 ? candidates[0].Path : "",
            Parameters = candidates.Length == 1
                ? Get(candidates[0].Method, "Parameters")
                    .EnumerateArray()
                    .Select(parameter => (object)new
                    {
                        Name = Text(parameter, "Name"),
                        Type = Text(parameter, "Type"),
                    })
                    .ToArray()
                : Array.Empty<object>(),
        };
    }
    sealed class ParameterMetadataProjection
    {
        public string Status { get; set; } = "";
        public string Source { get; set; } = "";
        public object[] Parameters { get; set; } = Array.Empty<object>();
    }
    static object[] ProjectOperationAliases(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray()
                .Select(x => (object)new
                {
                    methodInfo = Text(x, "methodInfo"),
                    name = Text(x, "name"),
                    parameterCount = Number(x, "parameterCount"),
                    isSelected = Bool(x, "isSelected"),
                })
                .ToArray()
            : Array.Empty<object>();    sealed record NativeAlias(string Name, string Pointer, string Info, int Arity);
    sealed class NativeTarget
    {
        public string DeclaringType { get; set; } = ""; public string Method { get; set; } = "";
        public string ReturnType { get; set; } = ""; public string[] ParameterTypes { get; set; } = Array.Empty<string>();
        public string MethodInfoPointer { get; set; } = ""; public string MethodPointer { get; set; } = "";
    }
    sealed class NativeTargetResult
    {
        public string Command { get; set; } = ""; public string Input { get; set; } = "";
        public string Status { get; set; } = ""; public string RuntimeObservationSource { get; set; } = ""; public string MetadataInput { get; set; } = "";
        public NativeTarget Target { get; set; } = new(); public string Scope { get; set; } = "";
        public string ScopeBoundary { get; set; } = ""; public string Classification { get; set; } = "";
        public int ConflictCount { get; set; }
        public NativeAlias[] SamePointerAliases { get; set; } = Array.Empty<NativeAlias>();
        public NativeAlias[] DifferentPointerAliases { get; set; } = Array.Empty<NativeAlias>();
        public string[] EnumerationErrors { get; set; } = Array.Empty<string>();
        public object[] OperationTargetsObservationOnly { get; set; } = Array.Empty<object>();
    }
}
