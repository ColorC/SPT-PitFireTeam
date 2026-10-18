using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace pitTeam.Server.Routers.Static;

// Runs after the stock BuildStaticRouter. No manifest means byte-for-byte pass-through.
[Injectable(TypePriority = OnLoadOrder.Routers + 2)]
public sealed class ScenarioBuildsRouter(JsonUtil jsonUtil, TemplateTable templateTable, ISptLogger<ScenarioBuildsRouter> logger)
    : StaticRouter(jsonUtil,
    [
        new RouteAction<EmptyRequestData>("/client/builds/list",
            (url, info, sessionId, output, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string filtered = ScenarioBuildCatalog.Filter(sessionId.ToString(), output ?? string.Empty,
                    AppContext.BaseDirectory, templateTable.DefaultEquipmentPresets.Select(preset => preset.Id.ToString()),
                    message => logger.Warning(message));
                return new ValueTask<string>(filtered);
            }),
    ])
{ }

public static class ScenarioBuildCatalog
{
    public const int RequiredCatalogCount = 14;
    private const int MaximumManifestBytes = 65536;

    // Exposed separately so the real production filtering policy can be checked without HTTP or profiles.
    public static string Filter(string sessionId, string previousOutput, string runtimeRoot,
        IEnumerable<string> originalDefaultPresetIds, Action<string>? warning = null)
    {
        if (!IsMongoId(sessionId) || string.IsNullOrEmpty(previousOutput)) return previousOutput;
        string file = Path.Combine(runtimeRoot, "user", "mods", "pitFireTeam-ServerMod", "Resources", "scenarios", sessionId + ".json");
        if (!File.Exists(file)) return previousOutput;
        try
        {
            if (new FileInfo(file).Length > MaximumManifestBytes) throw new InvalidDataException("Manifest is too large.");
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(file)) as JsonObject
                ?? throw new InvalidDataException("Manifest must be an object.");
            if (manifest["schemaVersion"]?.GetValue<int>() != 1 || manifest["enabled"]?.GetValue<bool>() != true)
                return previousOutput;
            if (!string.Equals(manifest["profileId"]?.GetValue<string>(), sessionId, StringComparison.Ordinal))
                throw new InvalidDataException("Manifest profile ID does not match its account.");
            JsonArray ids = manifest["catalogBuildIds"] as JsonArray
                ?? throw new InvalidDataException("Initial catalog IDs are missing.");
            HashSet<string> initialIds = new(StringComparer.Ordinal);
            foreach (JsonNode? node in ids)
            {
                string? id = node?.GetValue<string>();
                if (!IsMongoId(id) || !initialIds.Add(id!)) throw new InvalidDataException("Catalog IDs must be unique MongoIDs.");
            }
            if (ids.Count != RequiredCatalogCount) throw new InvalidDataException("The scenario requires exactly fourteen catalog IDs.");

            JsonObject response = JsonNode.Parse(previousOutput) as JsonObject
                ?? throw new InvalidDataException("Build response must be an object.");
            if (response["err"]?.GetValue<int>() != 0) return previousOutput;
            if (response["data"] is not JsonObject data || data["equipmentBuilds"] is not JsonArray builds)
                throw new InvalidDataException("Stock equipment build list is missing.");
            // This is a local ID snapshot only. The stock controller appends cloned originals to the response.
            HashSet<string> originals = new(originalDefaultPresetIds, StringComparer.Ordinal);
            bool removed = false;
            for (int index = builds.Count - 1; index >= 0; index--)
            {
                string? id = builds[index]?["Id"]?.GetValue<string>();
                string? type = builds[index]?["BuildType"]?.GetValue<string>();
                // Initial catalog IDs validate the seed; later native Custom/Storage builds remain visible.
                if (id != null && originals.Contains(id) && string.Equals(type, "Standard", StringComparison.Ordinal))
                {
                    builds.RemoveAt(index);
                    removed = true;
                }
            }
            // Only a parsed response string is changed; neither SaveServer nor shared templates are mutated.
            return removed ? response.ToJsonString() : previousOutput;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or InvalidDataException or FormatException)
        {
            warning?.Invoke("[Scenario] Catalog manifest/filter rejected: " + error.GetType().Name + ": " + error.Message);
            return previousOutput;
        }
    }

    private static bool IsMongoId(string? value)
    {
        if (value == null || value.Length != 24) return false;
        foreach (char ch in value) if (!(ch is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        return true;
    }
}
