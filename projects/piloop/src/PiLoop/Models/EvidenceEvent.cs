using System.Text.Json.Serialization;

namespace PiLoop.Models;

[JsonConverter(typeof(JsonStringEnumConverter<EvidenceTarget>))]
public enum EvidenceTarget
{
    [JsonStringEnumMemberName("epic")]
    Epic,
    [JsonStringEnumMemberName("task")]
    Task,
}

[JsonConverter(typeof(JsonStringEnumConverter<EvidenceStatus>))]
public enum EvidenceStatus
{
    [JsonStringEnumMemberName("planned")]
    Planned,
    [JsonStringEnumMemberName("in_progress")]
    InProgress,
    [JsonStringEnumMemberName("completed")]
    Completed,
    [JsonStringEnumMemberName("blocked")]
    Blocked,
    [JsonStringEnumMemberName("failed")]
    Failed,
    [JsonStringEnumMemberName("verified")]
    Verified,
    [JsonStringEnumMemberName("decision")]
    Decision,
}

[JsonConverter(typeof(JsonStringEnumConverter<TestResultStatus>))]
public enum TestResultStatus
{
    [JsonStringEnumMemberName("passed")]
    Passed,
    [JsonStringEnumMemberName("failed")]
    Failed,
    [JsonStringEnumMemberName("skipped")]
    Skipped,
    [JsonStringEnumMemberName("not_run")]
    NotRun,
}

public sealed record EvidenceDecision(
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("why")] string Why,
    [property: JsonPropertyName("alternativesConsidered")] string[] AlternativesConsidered,
    [property: JsonPropertyName("tradeoff")] string Tradeoff);

public sealed record EvidenceBlocker(
    [property: JsonPropertyName("blocker")] string Blocker,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("mitigation")] string Mitigation);

public sealed record EvidenceTestResult(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("result")] TestResultStatus Result,
    [property: JsonPropertyName("evidence")] string Evidence);

public sealed record EvidenceRemainingIssue(
    [property: JsonPropertyName("issue")] string Issue,
    [property: JsonPropertyName("reasonDeferred")] string ReasonDeferred,
    [property: JsonPropertyName("nextStep")] string NextStep);

public sealed record EvidenceEvent(
    [property: JsonPropertyName("target")] EvidenceTarget Target,
    [property: JsonPropertyName("status")] EvidenceStatus Status,
    [property: JsonPropertyName("agent")] string Agent,
    [property: JsonPropertyName("step")] string Step,
    [property: JsonPropertyName("intent")] string Intent,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("workPerformed")] string[] WorkPerformed,
    [property: JsonPropertyName("decisions")] EvidenceDecision[] Decisions,
    [property: JsonPropertyName("blockers")] EvidenceBlocker[] Blockers,
    [property: JsonPropertyName("testResults")] EvidenceTestResult[] TestResults,
    [property: JsonPropertyName("remainingIssues")] EvidenceRemainingIssue[] RemainingIssues,
    [property: JsonPropertyName("artifacts")] string[] Artifacts,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("taskId")] string? TaskId = null);
