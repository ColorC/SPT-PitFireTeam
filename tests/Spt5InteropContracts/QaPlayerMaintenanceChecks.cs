using System.Text.Json;
using pitTeam.Compat;

internal static class QaPlayerMaintenanceChecks
{
    public static int Run()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "qa-player-maintenance-cases.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        int checkedTicks = 0;
        foreach (JsonElement scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            string name = scenario.GetProperty("name").GetString()!;
            bool guardSatisfied = scenario.GetProperty("protectionGuardSatisfied").GetBoolean();
            int calls = 0;
            string? lastSkipReason = null;
            foreach (JsonElement tick in scenario.GetProperty("maintenanceRequestedSequence").EnumerateArray())
            {
                bool requested = tick.GetBoolean();
                if (Spt5QaHealthMaintenancePolicy.ShouldInvokeFullHealthMaintenance(requested, guardSatisfied))
                {
                    calls++;
                    lastSkipReason = null;
                }
                else
                {
                    lastSkipReason = Spt5QaHealthMaintenancePolicy.SkipReason(requested, guardSatisfied);
                }
                checkedTicks++;
            }
            int expectedCalls = scenario.GetProperty("expectedCalls").GetInt32();
            string? expectedSkipReason = scenario.TryGetProperty("expectedFinalSkipReason", out JsonElement expectedReason)
                ? expectedReason.GetString()
                : null;
            if (calls != expectedCalls || lastSkipReason != expectedSkipReason)
                throw new InvalidOperationException($"{name}: calls={calls}, skipReason={lastSkipReason ?? "<null>"}");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, scenarios = document.RootElement.GetProperty("scenarios").GetArrayLength(), checkedTicks }));
        return 0;
    }
}
