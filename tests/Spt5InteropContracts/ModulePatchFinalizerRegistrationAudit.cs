using System.Text.Json;

internal static partial class Program
{
    static int ModulePatchFinalizerRegistrationSelfTest(Args args)
    {
        string sourcePath = Path.GetFullPath(args.R("source"));
        string fixturePath = Path.GetFullPath(args.G("fixtures") ?? Path.Combine(AppContext.BaseDirectory, "fixtures", "modulepatch-finalizer-registration-cases.json"));
        string source = System.IO.File.ReadAllText(sourcePath);
        string[] requiredSource =
        {
            "FindAttributedMethod(type, typeof(PatchPrefixAttribute))",
            "FindAttributedMethod(type, typeof(PatchPostfixAttribute))",
            "ReadOwnedHarmonyInstance(patch)",
            "field.FieldType != typeof(Harmony)",
            "owner = ownedHarmony.Id",
            "prefixOwner = prefix == null ? null : FindUniqueOwner",
            "postfixOwner = postfix == null ? null : FindUniqueOwner",
            "beforeFinalizerCount == 0",
            "beforeFinalizerCount > 1",
            "registrar.Patch(target, null, null, null, new HarmonyMethod(finalizer), null)",
            "prefixInventory == DescribePatches(after?.Prefixes)",
            "postfixInventory == DescribePatches(after?.Postfixes)",
            "beforePrefixCount == afterPrefixCount",
            "beforePostfixCount == afterPostfixCount",
        };
        var missing = requiredSource.Where(x => !source.Contains(x, StringComparison.Ordinal)).ToArray();
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(fixturePath));
        var cases = doc.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var results = new List<object>();
        bool passed = missing.Length == 0;
        foreach (var item in cases)
        {
            string name = item.GetProperty("name").GetString()!;
            var actual = EvaluateRegistrationCase(item);
            bool expectedAllowed = item.GetProperty("expectRegistered").GetBoolean();
            string expectedOwnerSource = item.GetProperty("expectedOwnerSource").GetString()!;
            string? expectedOwner = item.TryGetProperty("expectedOwner", out var ownerNode) && ownerNode.ValueKind == JsonValueKind.String ? ownerNode.GetString() : null;
            bool expectedAdd = item.GetProperty("expectAddFinalizer").GetBoolean();
            bool ok = actual.Allowed == expectedAllowed && actual.OwnerSource == expectedOwnerSource && actual.Owner == expectedOwner && actual.AddFinalizer == expectedAdd;
            passed &= ok;
            results.Add(new { name, expectedAllowed, actual.Allowed, expectedOwnerSource, actual.OwnerSource, expectedOwner, actual.Owner, expectedAdd, actual.AddFinalizer, passed = ok, actual.Reason });
        }
        var report = new { command = "modulepatch-finalizer-registration-self-test", fixture = fixturePath, source = sourcePath, sourceAssertions = requiredSource.Length, missingSourceAssertions = missing, caseCount = cases.Length, passed, cases = results };
        Console.WriteLine(JsonSerializer.Serialize(report, J));
        return passed ? 0 : 1;
    }

    static (bool Allowed, string? OwnerSource, string? Owner, bool AddFinalizer, string Reason) EvaluateRegistrationCase(JsonElement item)
    {
        var hooks = item.GetProperty("declaredHooks").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        string? owner = null;
        string ownerSource;
        bool patchInfoNull = item.GetProperty("getPatchInfoNull").GetBoolean();
        if (hooks.Count == 0)
        {
            int fields = item.GetProperty("liveHarmonyFieldMatches").GetInt32();
            string? id = item.TryGetProperty("liveHarmonyId", out var idNode) && idNode.ValueKind == JsonValueKind.String ? idNode.GetString() : null;
            if (fields != 1 || String.IsNullOrWhiteSpace(id)) return (false, "none", null, false, "finalizer-only requires one live Harmony field and nonempty Id");
            owner = id;
            ownerSource = "modulepatch-harmony-id";
        }
        else
        {
            if (patchInfoNull) return (false, "none", null, false, "patch info is null although source declares prefix/postfix");
            var owners = new List<string>();
            foreach (string hook in hooks)
            {
                string key = hook == "prefix" ? "runtimePrefixOwners" : "runtimePostfixOwners";
                var matches = item.GetProperty(key).EnumerateArray().Select(x => x.GetString()!).ToArray();
                if (matches.Length != 1 || String.IsNullOrWhiteSpace(matches[0])) return (false, "none", null, false, "expected one exact runtime hook method match");
                owners.Add(matches[0]);
            }
            if (owners.Distinct(StringComparer.Ordinal).Count() != 1) return (false, "none", null, false, "prefix/postfix runtime owners differ");
            owner = owners[0];
            ownerSource = hooks.Contains("prefix") ? "runtime-prefix" : "runtime-postfix";
        }
        int existing = item.GetProperty("existingOwnerFinalizerCount").GetInt32();
        if (existing < 0 || existing > 1) return (false, "none", null, false, "duplicate existing owner/method finalizer");
        return (true, ownerSource, owner, existing == 0, existing == 0 ? "register finalizer once" : "idempotent existing finalizer");
    }
}
