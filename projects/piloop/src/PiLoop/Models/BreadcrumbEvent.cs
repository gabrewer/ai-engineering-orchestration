using System.Text.Json.Serialization;

namespace PiLoop.Models;

[JsonConverter(typeof(JsonStringEnumConverter<BreadcrumbTarget>))]
public enum BreadcrumbTarget
{
    [JsonStringEnumMemberName("epic")]
    Epic,
    [JsonStringEnumMemberName("task")]
    Task,
}

[JsonConverter(typeof(JsonStringEnumConverter<BreadcrumbStatus>))]
public enum BreadcrumbStatus
{
    [JsonStringEnumMemberName("started")]
    Started,
    [JsonStringEnumMemberName("completed")]
    Completed,
    [JsonStringEnumMemberName("failed")]
    Failed,
    [JsonStringEnumMemberName("blocked")]
    Blocked,
    [JsonStringEnumMemberName("changes_needed")]
    ChangesNeeded,
    [JsonStringEnumMemberName("escalated")]
    Escalated,
    [JsonStringEnumMemberName("verified")]
    Verified,
    [JsonStringEnumMemberName("decision")]
    Decision,
}

public sealed record BreadcrumbEvent(
    [property: JsonPropertyName("target")] BreadcrumbTarget Target,
    [property: JsonPropertyName("step")] string Step,
    [property: JsonPropertyName("agent")] string Agent,
    [property: JsonPropertyName("status")] BreadcrumbStatus Status,
    [property: JsonPropertyName("whatHappened")] string WhatHappened,
    [property: JsonPropertyName("why")] string Why,
    [property: JsonPropertyName("alternativesConsidered")] string[] AlternativesConsidered,
    [property: JsonPropertyName("confidence")] PiWorkerConfidence Confidence,
    [property: JsonPropertyName("nextAction")] string NextAction,
    [property: JsonPropertyName("artifacts")] string[]? Artifacts = null,
    [property: JsonPropertyName("verification")] string? Verification = null,
    [property: JsonPropertyName("taskId")] string? TaskId = null);
