using System.Security.Cryptography;
using System.Text.Json;
using PiLoop.Models;

namespace PiLoop.Services;

public sealed class PlanManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly DirectoryInfo _targetRoot;
    private readonly string _manifestPath;

    public PlanManifestService(DirectoryInfo targetRoot)
    {
        _targetRoot = targetRoot;
        _manifestPath = Path.Combine(targetRoot.FullName, "docs", "sprints", "plan-manifest.json");
    }

    public string ManifestPath => _manifestPath;

    public async Task<PlanManifest> LoadAsync()
    {
        if (!File.Exists(_manifestPath))
            return new PlanManifest();

        try
        {
            var json = await File.ReadAllTextAsync(_manifestPath);
            return JsonSerializer.Deserialize<PlanManifest>(json, JsonOptions) ?? new PlanManifest();
        }
        catch
        {
            return new PlanManifest();
        }
    }

    public async Task SaveAsync(PlanManifest manifest)
    {
        manifest.UpdatedAt = DateTime.UtcNow.ToString("O");
        Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(_manifestPath, json + Environment.NewLine);
    }

    public PlanManifestEntry? Find(PlanManifest manifest, string prdHash, string logicalSprint) =>
        manifest.Entries.LastOrDefault(e =>
            string.Equals(e.PrdHash, prdHash, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.LogicalSprint, logicalSprint, StringComparison.OrdinalIgnoreCase));

    public PlanManifestEntry Upsert(
        PlanManifest manifest,
        string prdPath,
        string prdHash,
        string logicalSprint,
        string sprint,
        string? briefPath,
        string planPath,
        string runTimestamp,
        IssueMap? githubIssues)
    {
        var existing = Find(manifest, prdHash, logicalSprint);
        if (existing is null)
        {
            existing = new PlanManifestEntry
            {
                PrdPath = NormalizeRelativePath(prdPath),
                PrdHash = prdHash,
                LogicalSprint = logicalSprint,
                Sprint = sprint,
                BriefPath = NormalizeNullableRelativePath(briefPath),
                PlanPath = NormalizeRelativePath(planPath),
                LastRunTimestamp = runTimestamp,
                GitHubIssues = githubIssues,
            };
            manifest.Entries.Add(existing);
            return existing;
        }

        existing.PrdPath = NormalizeRelativePath(prdPath);
        existing.Sprint = sprint;
        existing.BriefPath = NormalizeNullableRelativePath(briefPath) ?? existing.BriefPath;
        existing.PlanPath = NormalizeRelativePath(planPath);
        existing.LastRunTimestamp = runTimestamp;
        existing.GitHubIssues = githubIssues ?? existing.GitHubIssues;
        return existing;
    }

    public async Task<string> ComputeFileHashAsync(string path)
    {
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_targetRoot.FullName, path);
        await using var stream = File.OpenRead(fullPath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string? FindLatestBriefForLogicalSprint(string logicalSprint)
    {
        var sprintsDir = Path.Combine(_targetRoot.FullName, "docs", "sprints");
        if (!Directory.Exists(sprintsDir))
            return null;

        return Directory.GetFiles(sprintsDir, "*-brief-*.md")
            .Where(path => string.Equals(ExtractLogicalSprintFromBrief(path), logicalSprint, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => path)
            .Select(NormalizeRelativePath)
            .FirstOrDefault();
    }

    private static string ExtractLogicalSprintFromBrief(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var marker = name.IndexOf("-brief-", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
            name = name[..marker];

        var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && parts[0].Length == 8 && parts[0].All(char.IsDigit))
            return string.Join('-', parts[1..]);

        return name;
    }

    private string? NormalizeNullableRelativePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : NormalizeRelativePath(path);

    private string NormalizeRelativePath(string path)
    {
        var fullPath = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(_targetRoot.FullName, path));
        var relative = Path.GetRelativePath(_targetRoot.FullName, fullPath);
        return relative.Replace('\\', '/');
    }
}
