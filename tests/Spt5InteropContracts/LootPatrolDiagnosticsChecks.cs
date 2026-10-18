using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using pitTeam.Compat;

internal static class LootPatrolDiagnosticsChecks
{
    public sealed class LayerProbe { public string Pointer => "layer-pointer-fixture"; }
    public sealed class OwnerProbe
    {
        public string Pointer => "owner-pointer-fixture";
        public string ProfileId => "qa-profile-fixture";
        public ProfileProbe Profile => new();
    }
    public sealed class ProfileProbe { public string AccountId => "qa-account-fixture"; }
    
    public sealed class ThrowingOwner
    {
        public string Pointer => throw new InvalidOperationException("pointer-getter-fixture");
        public string ProfileId => throw new InvalidOperationException("profile-getter-fixture");
        public ProfileProbe Profile => throw new InvalidOperationException("account-getter-fixture");
    }

    public static int Run(string[] args)
    {
        string fixturePath = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(AppContext.BaseDirectory, "fixtures", "loot-patrol-diagnostics-cases.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        if (fixture.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || fixture.RootElement.GetProperty("cases").GetArrayLength() != 4)
            throw new InvalidDataException("LootPatrol diagnostics fixture schema/case count mismatch: " + fixturePath);
        string? old = Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT");
        Environment.SetEnvironmentVariable("PIT_SPT5_PROBE_ROOT", "fixture-enabled");
        try
        {
            JsonElement first = Record(Thrown(new NullReferenceException("GetDecision NRE fixture; source shape only", new InvalidOperationException("inner cause fixture"))), new LayerProbe(), new OwnerProbe());
            Assert(Text(first, "exceptionType") == "System.NullReferenceException" && Text(first, "exceptionDetails")?.Contains("GetDecision NRE fixture") == true && Text(first, "exceptionStackTrace") != null, "raw NRE details/stack");
            Assert(Text(first, "innerExceptionType") == "System.InvalidOperationException" && Text(first, "innerExceptionMessage") == "inner cause fixture" && Text(first, "innerExceptionChain")?.Contains("inner cause fixture") == true, "inner cause retained");
            Assert(Text(first, "layerProxyType")?.EndsWith("LayerProbe", StringComparison.Ordinal) == true && Text(first, "ownerProxyType")?.EndsWith("OwnerProbe", StringComparison.Ordinal) == true, "native types");
            Assert(Text(first, "ownerProfileId") == "qa-profile-fixture" && Text(first, "ownerAccountId") == "qa-account-fixture", "known owner IDs");
            Assert(Text(first, "ownerNativeType") == null && Text(first, "ownerNativeTypeStatus") == "unavailable-no-verified-native-type-accessor" && Text(first, "ownerProxyTypeStatus") == "managed-proxy-type", "proxy/native type distinction");
            Assert(Text(first, "targetIdentityStatus") == "unavailable" && !string.IsNullOrWhiteSpace(Text(first, "targetIdentityStage")), "target explicitly unavailable");

            JsonElement noOwner = Record(Thrown(new NullReferenceException("no-owner fixture")), new LayerProbe(), null);
            Assert(noOwner.GetProperty("ownerProfileId").ValueKind == JsonValueKind.Null && Text(noOwner, "ownerProfileIdStatus") == "unavailable" && Text(noOwner, "ownerAccountIdStatus") == "unavailable" && Text(noOwner, "ownerNativeTypeStatus") == "unavailable-no-verified-native-type-accessor" && noOwner.GetProperty("confirmedFollower").ValueKind == JsonValueKind.Null, "no-owner unknown");

            JsonElement getterFailure = Record(Thrown(new NullReferenceException("getter failure fixture")), new LayerProbe(), new ThrowingOwner());
            Assert(Text(getterFailure, "ownerProfileIdStatus") == "error" && Text(getterFailure, "ownerAccountIdStatus") == "error" && Text(getterFailure, "captureError")?.Contains("owner-pointer") == true, "getter failures captured");

            JsonElement longError = Record(Thrown(new NullReferenceException(new string('x', 10000))), new LayerProbe(), new OwnerProbe());
            Assert(longError.GetProperty("exceptionDetailsTruncated").GetBoolean() && Text(longError, "exceptionDetails")?.Length == 8192 && Text(longError, "exceptionStackTrace")?.Length <= 4096, "bounded text truncation");
            Console.WriteLine(JsonSerializer.Serialize(new { passed = true, fixture = fixturePath, cases = new[] { "raw-nre-stack-inner-and-owner-identity", "no-owner-unknown", "throwing-getters-captured", "bounded-truncation" }, nativeValidation = false }));
            return 0;
        }
        finally { Environment.SetEnvironmentVariable("PIT_SPT5_PROBE_ROOT", old); }
    }

    static Exception Thrown(Exception ex) { try { throw ex; } catch (Exception caught) { return caught; } }
    static JsonElement Record(Exception ex, object layer, object? owner)
    {
        Thread.Sleep(1050);
        Spt5LootPatrolDiagnostics.RecordException(ex, () => Spt5LootPatrolDiagnostics.Capture(layer, owner, "fixture-stage", null, "fixture-fallback", true, null, () => owner == null ? (bool?)null : true));
        using var snapshot = JsonDocument.Parse(JsonSerializer.Serialize(Spt5LootPatrolDiagnostics.Snapshot()));
        return snapshot.RootElement.GetProperty("samples").EnumerateArray().Last().Clone();
    }
    static string? Text(JsonElement item, string property) => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException("LootPatrol diagnostics self-test failed: " + name); }
}
