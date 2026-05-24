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
            var parsed = JsonSerializer.Deserialize<Dictionary<string, SkillModelEntry>>(json, JsonOptions) ?? [];
            return new Dictionary<string, SkillModelEntry>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, SkillModelEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
