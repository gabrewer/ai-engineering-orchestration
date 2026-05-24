using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PiLoop.Models;

namespace PiLoop.Services;

public sealed class PiRpcRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly PiProcessHost _processHost;
    private readonly PiRuntimeOptions _runtimeOptions;

    public PiRpcRunner(PiRuntimeOptions runtimeOptions, PiProcessHost? processHost = null)
    {
        _runtimeOptions = runtimeOptions;
        _processHost = processHost ?? new PiProcessHost(runtimeOptions.PiCommand);
    }

    public async Task<PiWorkerRunResult> RunPromptAsync(
        string workingDirectory,
        string prompt,
        TimeSpan timeout,
        IEnumerable<string>? additionalPiArgs = null,
        CancellationToken cancellationToken = default)
    {
        PiWorkerRunResult? lastResult = null;

        for (var attempt = 1; attempt <= _runtimeOptions.MaxAttempts; attempt++)
        {
            PiWorkerRunResult result;
            try
            {
                result = await RunPromptAttemptAsync(
                    workingDirectory,
                    prompt,
                    timeout,
                    attempt,
                    additionalPiArgs,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                result = new PiWorkerRunResult(
                    null,
                    string.Empty,
                    [],
                    [
                        $"# runner attempt {attempt}/{_runtimeOptions.MaxAttempts}",
                        $"# diagnostic: attempts={attempt}; completed=False; exitCode=-1; failureKind={PiFailureKind.Process}; error={ex.Message}"
                    ],
                    -1,
                    ex.Message,
                    false,
                    PiFailureKind.Process,
                    attempt);
            }

            lastResult = result;
            if (result.Succeeded)
                return result;

            if (!ShouldRetry(result, attempt))
                return result;

            var delay = GetRetryDelay(attempt);
            await Task.Delay(delay, cancellationToken);
        }

        return lastResult ?? new PiWorkerRunResult(
            null,
            string.Empty,
            [],
            [],
            -1,
            "Pi worker failed before producing a result.",
            false,
            PiFailureKind.Process,
            0);
    }

    private async Task<PiWorkerRunResult> RunPromptAttemptAsync(
        string workingDirectory,
        string prompt,
        TimeSpan timeout,
        int attempt,
        IEnumerable<string>? additionalPiArgs,
        CancellationToken cancellationToken)
    {
        using var process = _processHost.StartRpc(workingDirectory, _runtimeOptions, additionalPiArgs);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var rawLines = new List<string>
        {
            $"# runner attempt {attempt}/{_runtimeOptions.MaxAttempts}",
            $"# pi command: {_runtimeOptions.PiCommand}",
            $"# provider: {_runtimeOptions.Provider ?? "default"}",
            $"# model: {_runtimeOptions.Model ?? "default"}",
        };
        var events = new List<PiRpcEvent>();
        var assistantText = new StringBuilder();
        PiWorkerResult? finalResult = null;
        string? errorMessage = null;
        var completed = false;

        try
        {
            await SendCommandAsync(process.StandardInput, new
            {
                id = "prompt-1",
                type = "prompt",
                message = prompt,
            }, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            errorMessage = $"Pi worker timed out before the prompt was sent after {timeout}.";
            TryKill(process, rawLines);
            return BuildResult(finalResult, assistantText, events, rawLines, process, errorMessage, completed, attempt, stderr: null);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to send prompt to Pi RPC host: {ex.Message}";
            TryKill(process, rawLines);
            return BuildResult(finalResult, assistantText, events, rawLines, process, errorMessage, completed, attempt, stderr: null);
        }

        try
        {
            while (!timeoutCts.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeoutCts.Token);
                if (line is null) break;

                rawLines.Add(line);
                if (string.IsNullOrWhiteSpace(line)) continue;

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var type = root.GetProperty("type").GetString();

                if (string.Equals(type, "response", StringComparison.OrdinalIgnoreCase))
                {
                    var response = JsonSerializer.Deserialize<PiRpcResponse>(root.GetRawText(), JsonOptions);
                    if (response is not null && !response.Success)
                        errorMessage ??= response.Error ?? $"RPC command '{response.Command}' failed.";
                    continue;
                }

                var rpcEvent = JsonSerializer.Deserialize<PiRpcEvent>(root.GetRawText(), JsonOptions);
                if (rpcEvent is not null)
                    events.Add(rpcEvent);

                CaptureAssistantText(root, assistantText);

                if (string.Equals(type, "agent_end", StringComparison.OrdinalIgnoreCase))
                {
                    finalResult = TryExtractFinalResult(assistantText.ToString());
                    completed = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            errorMessage ??= $"Pi worker timed out after {timeout}.";
            TryKill(process, rawLines);
        }
        catch (JsonException ex)
        {
            errorMessage ??= $"Pi RPC produced malformed JSON: {ex.Message}";
            TryKill(process, rawLines);
        }
        catch (Exception ex)
        {
            errorMessage ??= ex.Message;
            TryKill(process, rawLines);
        }

        if (completed)
            TryKill(process, rawLines);

        string stderr;
        try
        {
            stderr = await process.StandardError.ReadToEndAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            rawLines.Add($"# runner stderr read failed: {ex.Message}");
            stderr = string.Empty;
        }

        return BuildResult(finalResult, assistantText, events, rawLines, process, errorMessage, completed, attempt, stderr);
    }

    private static PiWorkerRunResult BuildResult(
        PiWorkerResult? finalResult,
        StringBuilder assistantText,
        List<PiRpcEvent> events,
        List<string> rawLines,
        System.Diagnostics.Process process,
        string? errorMessage,
        bool completed,
        int attempt,
        string? stderr)
    {
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            rawLines.Add("# stderr");
            rawLines.AddRange(stderr.Split('\n').Select(x => x.TrimEnd('\r')));
            if (errorMessage is null)
                errorMessage = FirstNonEmptyLine(stderr);
        }

        try
        {
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            rawLines.Add($"# runner wait-for-exit failed: {ex.Message}");
            TryKill(process, rawLines);
        }

        var exitCode = process.HasExited ? process.ExitCode : -1;
        var failureKind = ClassifyFailure(errorMessage, rawLines, exitCode, completed, finalResult is not null);
        var diagnosticSummary = $"# diagnostic: attempts={attempt}; completed={completed}; exitCode={exitCode}; failureKind={failureKind}; error={errorMessage ?? "none"}";
        rawLines.Insert(0, diagnosticSummary);

        return new PiWorkerRunResult(
            finalResult,
            assistantText.ToString().Trim(),
            events,
            rawLines,
            exitCode,
            errorMessage,
            completed,
            failureKind,
            attempt);
    }

    private bool ShouldRetry(PiWorkerRunResult result, int attempt)
    {
        if (attempt >= _runtimeOptions.MaxAttempts)
            return false;

        return result.FailureKind is PiFailureKind.Transport;
    }

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(10, 2 * attempt));

    private static PiFailureKind ClassifyFailure(string? errorMessage, IReadOnlyList<string> rawLines, int exitCode, bool completed, bool hasFinalResult)
    {
        if (completed && hasFinalResult && string.IsNullOrWhiteSpace(errorMessage))
            return PiFailureKind.None;

        if (completed && !hasFinalResult && string.IsNullOrWhiteSpace(errorMessage))
            return PiFailureKind.Rpc;

        var diagnosticLines = rawLines
            .Where(line => !line.Contains("encrypted_content", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.Contains("thinkingSignature", StringComparison.OrdinalIgnoreCase));
        var haystack = string.Join('\n', diagnosticLines) + "\n" + (errorMessage ?? string.Empty);
        haystack = haystack.ToUpperInvariant();

        if (haystack.Contains("INSUFFICIENT_QUOTA") || haystack.Contains("QUOTA EXCEEDED") || haystack.Contains("BILLING") || haystack.Contains("USAGE LIMIT"))
            return PiFailureKind.Quota;

        if (haystack.Contains("RATE LIMIT") || haystack.Contains("TOO MANY REQUESTS") || haystack.Contains("429"))
            return PiFailureKind.RateLimit;

        if (haystack.Contains("UNAUTHORIZED") || haystack.Contains("AUTHENTICATION") || haystack.Contains("INVALID API KEY") || haystack.Contains("401") || haystack.Contains("FORBIDDEN") || haystack.Contains("403"))
            return PiFailureKind.Auth;

        if (haystack.Contains("TIMED OUT") || haystack.Contains("TIMEOUT"))
            return PiFailureKind.Timeout;

        if (haystack.Contains("WEBSOCKET ERROR") || haystack.Contains("PROVIDER_TRANSPORT_FAILURE") || haystack.Contains("TRANSPORT") || haystack.Contains("ECONNRESET") || haystack.Contains("SOCKET") || haystack.Contains("CONNECTION RESET") || haystack.Contains("NETWORK ERROR"))
            return PiFailureKind.Transport;

        if (haystack.Contains("RPC COMMAND") || haystack.Contains("MALFORMED JSON"))
            return PiFailureKind.Rpc;

        return exitCode != 0 ? PiFailureKind.Process : PiFailureKind.Unknown;
    }

    internal static PiWorkerResult? TryExtractFinalResult(string assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText)) return null;

        const string fence = "```json";
        var start = assistantText.LastIndexOf(fence, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        start += fence.Length;
        var end = assistantText.IndexOf("```", start, StringComparison.Ordinal);
        if (end < 0) return null;

        var json = assistantText[start..end].Trim();
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<PiWorkerResult>(json, JsonOptions);
        }
        catch
        {
            try
            {
                return JsonSerializer.Deserialize<PiWorkerResult>(NormalizeWorkerResultJson(json), JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string NormalizeWorkerResultJson(string json)
    {
        var node = JsonNode.Parse(json)?.AsObject()
            ?? throw new JsonException("Worker result JSON was not an object.");

        if (node["artifacts"] is JsonArray artifacts)
        {
            foreach (var artifactNode in artifacts.OfType<JsonObject>())
            {
                var kind = artifactNode["kind"]?.GetValue<string>();
                if (!IsKnownArtifactKind(kind))
                    artifactNode["kind"] = "doc";
            }
        }

        return node.ToJsonString();
    }

    private static bool IsKnownArtifactKind(string? kind) =>
        kind is "code" or "test" or "doc" or "plan" or "contract" or "log";

    private static async Task SendCommandAsync(TextWriter writer, object command, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(command, JsonOptions);
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    private static void CaptureAssistantText(JsonElement root, StringBuilder assistantText)
    {
        var type = root.GetProperty("type").GetString();

        if (string.Equals(type, "message_update", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("assistantMessageEvent", out var assistantMessageEvent) &&
            assistantMessageEvent.TryGetProperty("type", out var eventType) &&
            string.Equals(eventType.GetString(), "text_delta", StringComparison.OrdinalIgnoreCase) &&
            assistantMessageEvent.TryGetProperty("delta", out var delta))
        {
            assistantText.Append(delta.GetString());
            return;
        }

        if (string.Equals(type, "message_end", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("message", out var message))
        {
            var text = ExtractAssistantMessageText(message);
            if (!string.IsNullOrWhiteSpace(text))
            {
                assistantText.Clear();
                assistantText.Append(text);
            }
            return;
        }

        if (string.Equals(type, "agent_end", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("messages", out var messages) &&
            messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in messages.EnumerateArray())
            {
                var text = ExtractAssistantMessageText(candidate);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    assistantText.Clear();
                    assistantText.Append(text);
                }
            }
        }
    }

    private static string ExtractAssistantMessageText(JsonElement message)
    {
        if (!message.TryGetProperty("role", out var role) || !string.Equals(role.GetString(), "assistant", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (!block.TryGetProperty("type", out var blockType) || !string.Equals(blockType.GetString(), "text", StringComparison.OrdinalIgnoreCase))
                continue;

            if (block.TryGetProperty("text", out var text))
                builder.Append(text.GetString());
        }

        return builder.ToString().Trim();
    }

    private static string? FirstNonEmptyLine(string text) =>
        text.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0);

    private static void TryKill(System.Diagnostics.Process process, List<string> rawLines)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            rawLines.Add($"# runner kill failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Failed to kill Pi process: {ex.Message}");
        }
    }
}
