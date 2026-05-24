using System.Text.Json.Serialization;

namespace PiLoop.Models;

[JsonConverter(typeof(JsonStringEnumConverter<PiWorkerStatus>))]
public enum PiWorkerStatus
{
    [JsonStringEnumMemberName("success")]
    Success,
    [JsonStringEnumMemberName("changes_needed")]
    ChangesNeeded,
    [JsonStringEnumMemberName("blocked")]
    Blocked,
    [JsonStringEnumMemberName("escalate")]
    Escalate,
    [JsonStringEnumMemberName("failed")]
    Failed,
}

[JsonConverter(typeof(JsonStringEnumConverter<PiArtifactKind>))]
public enum PiArtifactKind
{
    [JsonStringEnumMemberName("code")]
    Code,
    [JsonStringEnumMemberName("test")]
    Test,
    [JsonStringEnumMemberName("doc")]
    Doc,
    [JsonStringEnumMemberName("plan")]
    Plan,
    [JsonStringEnumMemberName("contract")]
    Contract,
    [JsonStringEnumMemberName("log")]
    Log,
}

[JsonConverter(typeof(JsonStringEnumConverter<PiWorkerConfidence>))]
public enum PiWorkerConfidence
{
    [JsonStringEnumMemberName("high")]
    High,
    [JsonStringEnumMemberName("medium")]
    Medium,
    [JsonStringEnumMemberName("low")]
    Low,
}

[JsonConverter(typeof(JsonStringEnumConverter<PiFindingSeverity>))]
public enum PiFindingSeverity
{
    [JsonStringEnumMemberName("critical")]
    Critical,
    [JsonStringEnumMemberName("high")]
    High,
    [JsonStringEnumMemberName("medium")]
    Medium,
    [JsonStringEnumMemberName("low")]
    Low,
}

public sealed record PiArtifact(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("kind")] PiArtifactKind Kind);

public sealed record PiFinding(
    [property: JsonPropertyName("severity")] PiFindingSeverity Severity,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("file")] string? File,
    [property: JsonPropertyName("line")] int? Line,
    [property: JsonPropertyName("recommendation")] string Recommendation);

public sealed record PiWorkerResult(
    [property: JsonPropertyName("status")] PiWorkerStatus Status,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("whatHappened")] string WhatHappened,
    [property: JsonPropertyName("why")] string Why,
    [property: JsonPropertyName("alternativesConsidered")] string[] AlternativesConsidered,
    [property: JsonPropertyName("confidence")] PiWorkerConfidence Confidence,
    [property: JsonPropertyName("nextAction")] string NextAction,
    [property: JsonPropertyName("artifacts")] PiArtifact[] Artifacts,
    [property: JsonPropertyName("findings")] PiFinding[] Findings)
{
    public static PiWorkerResult Blocked(string summary, string why) =>
        new(
            PiWorkerStatus.Blocked,
            summary,
            summary,
            why,
            ["none"],
            PiWorkerConfidence.Medium,
            "Human or orchestrator intervention required.",
            [],
            []);
}
