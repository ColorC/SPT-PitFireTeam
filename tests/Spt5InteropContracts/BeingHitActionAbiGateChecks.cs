using System.Text.Json;
using pitTeam.Compat;
internal static partial class Program
{
    static int BeingHitActionAbiGateSelfTest()
    {
        var cases = new[]
        {
            new { name = "windows-x64-large-native-value-positive", expected = true, actual = Spt5BeingHitActionAbiGate.SupportsRawByReferenceValue(true, true, 16u) },
            new { name = "windows-x64-size-eight-negative", expected = false, actual = Spt5BeingHitActionAbiGate.SupportsRawByReferenceValue(true, true, 8u) },
            new { name = "windows-x64-non-value-negative", expected = false, actual = Spt5BeingHitActionAbiGate.SupportsRawByReferenceValue(true, false, 16u) },
            new { name = "non-windows-large-value-negative", expected = false, actual = Spt5BeingHitActionAbiGate.SupportsRawByReferenceValue(false, true, 16u) },
        };
        var results = cases.Select(item => new { item.name, item.expected, item.actual, passed = item.expected == item.actual }).ToArray();
        bool passed = results.All(item => item.passed);
        Console.WriteLine(JsonSerializer.Serialize(new { command = "being-hit-action-abi-gate-self-test", passed, cases = results }, J));
        return passed ? 0 : 1;
    }
}