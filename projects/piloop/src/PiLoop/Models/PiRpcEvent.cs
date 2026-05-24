using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiLoop.Models;

public enum PiFailureKind
{
    None,
    Transport,
    Auth,
    RateLimit,
    Quota,
    Timeout,
    Process,
    Rpc,
    Unknown,
}

public sealed record PiRpcResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("command")] string? Command,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("data")] JsonElement? Data,
    [property: JsonPropertyName("id")] string? Id);

public sealed record PiRpcEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] JsonElement? Message,
    [property: JsonPropertyName("messages")] JsonElement? Messages,
    [property: JsonPropertyName("assistantMessageEvent")] JsonElement? AssistantMessageEvent,
    [property: JsonPropertyName("toolName")] string? ToolName,
    [property: JsonPropertyName("toolCallId")] string? ToolCallId,
    [property: JsonPropertyName("args")] JsonElement? Args,
    [property: JsonPropertyName("result")] JsonElement? Result,
    [property: JsonPropertyName("isError")] bool? IsError,
    [property: JsonPropertyName("error")] string? Error)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record PiWorkerRunResult(
    PiWorkerResult? FinalResult,
    string AssistantText,
    IReadOnlyList<PiRpcEvent> Events,
    IReadOnlyList<string> RawLines,
    int ExitCode,
    string? ErrorMessage,
    bool Completed,
    PiFailureKind FailureKind,
    int Attempts)
{
    public bool Succeeded => Completed && FinalResult is not null && ErrorMessage is null;

    public string DiagnosticSummary =>
        $"attempts={Attempts}; completed={Completed}; exitCode={ExitCode}; failureKind={FailureKind}; error={ErrorMessage ?? "none"}";
}
