using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiLoop.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TaskType>))]
public enum TaskType
{
    [JsonStringEnumMemberName("backend")]
    Backend,
    [JsonStringEnumMemberName("frontend")]
    Frontend,
    [JsonStringEnumMemberName("both")]
    Both,
}

public sealed record SprintPhases(
    [property: JsonPropertyName("domainModeling")] bool DomainModeling = true,
    [property: JsonPropertyName("apiContract")] bool ApiContract = true
);

public sealed record PrdTask(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] TaskType Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("acceptanceCriteria")] string[] AcceptanceCriteria,
    [property: JsonPropertyName("skipTests")] bool SkipTests = false
);

public sealed record Prd(
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("tasks")] PrdTask[] Tasks,
    [property: JsonPropertyName("phases")] SprintPhases? Phases = null,
    [property: JsonPropertyName("order")] int Order = 0
)
{
    public SprintPhases EffectivePhases => Phases ?? new SprintPhases();
}

public static class PrdReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Reads a sprint PRD. Accepts either an exact filename or a sprint name.
    /// If a sprint name is given (e.g., "foundation"), finds the most recent
    /// matching file: docs/prds/foundation-*.json or docs/prds/foundation.json.
    /// </summary>
    public static async Task<Prd> ReadAsync(string sprint)
    {
        var prdsDir = Path.Combine(StateManager.RepoRoot, "docs", "sprints");
        var path = ResolveSprintFile(prdsDir, sprint);

        var json = await File.ReadAllTextAsync(path);
        var prd = JsonSerializer.Deserialize<Prd>(json, JsonOptions)
            ?? throw new InvalidOperationException($"{path} deserialized to null.");

        if (string.IsNullOrWhiteSpace(prd.Sprint) || prd.Tasks is not { Length: > 0 })
            throw new InvalidOperationException($"{path} looks malformed — expected sprint and tasks fields.");

        return prd;
    }

    /// <summary>
    /// Discovers all sprint plan JSON files in docs/sprints/, sorted by the "order" field
    /// inside each JSON. Groups by sprint name and takes the most recent file per sprint.
    /// </summary>
    public static string[] DiscoverAllSprintPlans()
    {
        var sprintsDir = Path.Combine(StateManager.RepoRoot, "docs", "sprints");
        if (!Directory.Exists(sprintsDir))
            throw new DirectoryNotFoundException($"No sprints directory found at {sprintsDir}. Run the plan loop first.");

        // Find all JSON files, excluding questions/answers
        var allJsons = Directory.GetFiles(sprintsDir, "*.json")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return !name.StartsWith("questions", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(f => f) // most recent first within each group
            .ToArray();

        if (allJsons.Length == 0)
            throw new FileNotFoundException($"No sprint plan JSON files found in {sprintsDir}. Run the plan loop first.");

        // Group by logical sprint identity, take most recent plan per sprint.
        // Example: both 20260517-foundation-*.json and 20260524-foundation-*.json map to "foundation".
        var grouped = allJsons
            .GroupBy(ExtractLogicalSprintName)
            .Select(g => g.First()) // most recent per logical sprint
            .ToArray();

        // Read each JSON to get the order field, then sort by it
        var ordered = grouped
            .Select(f =>
            {
                try
                {
                    var json = File.ReadAllText(f);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var order = doc.RootElement.TryGetProperty("order", out var o) ? o.GetInt32() : 999;
                    return (file: f, order);
                }
                catch
                {
                    return (file: f, order: 999);
                }
            })
            .OrderBy(x => x.order)
            .ThenBy(x => Path.GetFileName(x.file))
            .Select(x => x.file)
            .ToArray();

        return ordered;
    }

    /// <summary>
    /// Extracts the sprint file prefix from a filename, stripping the trailing run timestamp.
    /// "foundation-20260317-015712.json" → "foundation"
    /// "20260411-foundation-20260411-015712.json" → "20260411-foundation"
    /// </summary>
    internal static string ExtractSprintPrefix(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var parts = name.Split('-');
        // Walk backwards to find where the trailing run timestamp starts (8-digit segment)
        for (var i = parts.Length - 2; i >= 1; i--)
        {
            if (parts[i].Length == 8 && parts[i].All(char.IsDigit))
                return string.Join('-', parts[..i]);
        }
        return name;
    }

    /// <summary>
    /// Extracts the logical sprint identity from a sprint file by stripping both the trailing
    /// run timestamp and any leading date prefix used for sortable filenames.
    /// "20260411-foundation-20260411-015712.json" → "foundation"
    /// "foundation-20260317-015712.json" → "foundation"
    /// </summary>
    public static string ExtractLogicalSprintName(string filePath)
    {
        var prefix = ExtractSprintPrefix(filePath);
        var parts = prefix.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && parts[0].Length == 8 && parts[0].All(char.IsDigit))
            return string.Join('-', parts[1..]);

        return prefix;
    }

    /// <summary>
    /// Resolves a sprint name to a JSON file. Accepts:
    /// - Exact file path: "docs/sprints/20260411-foundation-20260317.json"
    /// - Sprint name: "20260411-foundation" (finds most recent 20260411-foundation-*.json)
    /// - Short name: "foundation" (finds most recent *-foundation-*.json or *foundation*.json)
    /// </summary>
    private static string ResolveSprintFile(string sprintsDir, string sprint)
    {
        // Exact path provided, absolute or target-root-relative.
        var exactPath = Path.IsPathRooted(sprint) ? sprint : Path.Combine(StateManager.RepoRoot, sprint);
        if (File.Exists(exactPath))
            return exactPath;

        if (!Directory.Exists(sprintsDir))
            throw new FileNotFoundException(
                $"No sprints directory found at {sprintsDir}.");

        // Exact name: docs/sprints/<sprint>.json
        var exact = Path.Combine(sprintsDir, $"{sprint}.json");
        if (File.Exists(exact))
            return exact;

        // Timestamped with exact prefix: 20260411-foundation-*.json
        var candidates = Directory.GetFiles(sprintsDir, $"{sprint}-*.json")
            .OrderByDescending(f => f)
            .ToArray();

        if (candidates.Length > 0)
            return candidates[0];

        // Short name without date prefix: "foundation" matches "20260411-foundation-*.json"
        candidates = Directory.GetFiles(sprintsDir, $"*-{sprint}-*.json")
            .OrderByDescending(f => f)
            .ToArray();

        if (candidates.Length > 0)
            return candidates[0];

        // Broadest search: anything containing the sprint name
        candidates = Directory.GetFiles(sprintsDir, $"*{sprint}*.json")
            .Where(f => !f.Contains("-brief-"))  // exclude briefs
            .OrderByDescending(f => f)
            .ToArray();

        if (candidates.Length > 0)
            return candidates[0];

        throw new FileNotFoundException(
            $"No sprint plan found for '{sprint}' in {sprintsDir}. Run the plan loop first.");
    }
}
