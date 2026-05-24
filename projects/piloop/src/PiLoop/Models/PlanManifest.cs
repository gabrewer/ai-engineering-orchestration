using System.Text.Json.Serialization;

namespace PiLoop.Models;

public sealed class PlanManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("O");

    [JsonPropertyName("entries")]
    public List<PlanManifestEntry> Entries { get; set; } = [];
}

public sealed class PlanManifestEntry
{
    [JsonPropertyName("prdPath")]
    public required string PrdPath { get; set; }

    [JsonPropertyName("prdHash")]
    public required string PrdHash { get; set; }

    [JsonPropertyName("logicalSprint")]
    public required string LogicalSprint { get; set; }

    [JsonPropertyName("sprint")]
    public required string Sprint { get; set; }

    [JsonPropertyName("briefPath")]
    public string? BriefPath { get; set; }

    [JsonPropertyName("planPath")]
    public required string PlanPath { get; set; }

    [JsonPropertyName("lastRunTimestamp")]
    public required string LastRunTimestamp { get; set; }

    [JsonPropertyName("githubIssues")]
    public IssueMap? GitHubIssues { get; set; }
}
