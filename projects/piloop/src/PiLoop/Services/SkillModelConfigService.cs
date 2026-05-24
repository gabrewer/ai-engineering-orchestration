using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiLoop.Services;

public sealed record SkillModelEntry(
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("thinkingLevel")] string? ThinkingLevel);

public sealed class SkillModelConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly DirectoryInfo _targetRoot;
    private Dictionary<string, SkillModelEntry>? _config;

    public SkillModelConfigService(DirectoryInfo targetRoot)
    {
        _targetRoot = targetRoot;
    }

    public async Task<SkillModelEntry?> FindAsync(string workerName)
    {
        _config ??= await LoadAsync();
        return _config.TryGetValue(workerName, out var entry) ? entry : null;
    }

    private async Task<Dictionary<string, SkillModelEntry>> LoadAsync()
    {
        var path = Path.Combine(_targetRoot.FullName, ".pi", "skill-models.json");
        if (!File.Exists(path))
            return new Dictionary<string, SkillModelEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = await File.ReadAllTextAsync(path);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var result = new Dictionary<string, SkillModelEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var entry = property.Value.Deserialize<SkillModelEntry>(JsonOptions);
                if (entry is not null)
                    result[property.Name] = entry;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, SkillModelEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
