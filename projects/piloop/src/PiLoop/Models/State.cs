using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiLoop.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TaskStatus>))]
public enum TaskStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,
    [JsonStringEnumMemberName("testing")]
    Testing,
    [JsonStringEnumMemberName("building")]
    Building,
    [JsonStringEnumMemberName("destroying")]
    Destroying,
    [JsonStringEnumMemberName("reviewing")]
    Reviewing,
    [JsonStringEnumMemberName("committed")]
    Committed,
    [JsonStringEnumMemberName("done")]
    Done,
    [JsonStringEnumMemberName("failed")]
    Failed,
}

[JsonConverter(typeof(JsonStringEnumConverter<SprintPhaseStatus>))]
public enum SprintPhaseStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,
    [JsonStringEnumMemberName("running")]
    Running,
    [JsonStringEnumMemberName("done")]
    Done,
    [JsonStringEnumMemberName("failed")]
    Failed,
}

public sealed class SprintPhaseState
{
    [JsonPropertyName("domainModeling")]
    public SprintPhaseStatus DomainModeling { get; set; } = SprintPhaseStatus.Pending;

    [JsonPropertyName("apiContract")]
    public SprintPhaseStatus ApiContract { get; set; } = SprintPhaseStatus.Pending;

    [JsonPropertyName("pmSummary")]
    public SprintPhaseStatus PmSummary { get; set; } = SprintPhaseStatus.Pending;

    [JsonPropertyName("smokeTest")]
    public SprintPhaseStatus SmokeTest { get; set; } = SprintPhaseStatus.Pending;
}

public sealed class TaskState
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    [JsonPropertyName("lastFeedback")]
    public string? LastFeedback { get; set; }

    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; set; }
}

public sealed class IssueMap
{
    [JsonPropertyName("epicIssue")]
    public int? EpicIssue { get; set; }

    [JsonPropertyName("taskIssues")]
    public Dictionary<string, int> TaskIssues { get; set; } = new();
}

public sealed class RunState
{
    [JsonPropertyName("runId")]
    public required string RunId { get; set; }

    [JsonPropertyName("sprint")]
    public required string Sprint { get; set; }

    [JsonPropertyName("branch")]
    public required string Branch { get; set; }

    [JsonPropertyName("startedAt")]
    public required string StartedAt { get; set; }

    [JsonPropertyName("phases")]
    public SprintPhaseState Phases { get; set; } = new();

    [JsonPropertyName("tasks")]
    public Dictionary<string, TaskState> Tasks { get; set; } = new();

    [JsonPropertyName("issues")]
    public IssueMap Issues { get; set; } = new();
}

public static class StateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // Resolve state relative to the target project root. The CLI sets this for target-root
    // workflows before invoking runtime services.
    private static string _repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
    public static string RepoRoot => _repoRoot;
    public static string DotDir => Path.Combine(RepoRoot, ".piloop");

    public static void SetRepoRoot(string repoRoot) =>
        _repoRoot = Path.GetFullPath(repoRoot);

    private static string FindRepoRoot(string start)
    {
        var dir = start;
        while (!string.IsNullOrEmpty(dir))
        {
            // Main checkouts use a .git directory; worktrees use a .git file.
            // Both should anchor PiLoop state/logs to the checkout being run.
            var gitPath = Path.Combine(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath)) return dir;
            dir = Path.GetDirectoryName(dir)!;
        }
        return start;
    }
    private static string LogsDir => Path.Combine(DotDir, "logs");

    private static string StatePath(string sprint) => Path.Combine(DotDir, $"state-{sprint}.json");
    public static string PlannerDebugPath(string sprint) => Path.Combine(DotDir, $"{sprint}-planner-debug.log");

    public static void EnsureDotDir()
    {
        Directory.CreateDirectory(DotDir);
        Directory.CreateDirectory(LogsDir);
    }

    public static async Task<RunState?> LoadStateAsync(string sprint)
    {
        var path = StatePath(sprint);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var state = JsonSerializer.Deserialize<RunState>(json, JsonOptions);
            // Backfill for older state files
            state?.Phases ??= new SprintPhaseState();
            state?.Issues ??= new IssueMap();
            return state;
        }
        catch
        {
            return null;
        }
    }

    // Semaphore ensures serialized writes from concurrent tasks within a single run.
    // Parallel runs target different state files so no cross-run contention.
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public static async Task SaveStateAsync(RunState state)
    {
        await WriteLock.WaitAsync();
        try
        {
            EnsureDotDir();
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(StatePath(state.Sprint), json + "\n");
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static string GetLogPath(string runId, string taskId, string agent, int? attempt = null)
    {
        var runDir = Path.Combine(LogsDir, runId);
        Directory.CreateDirectory(runDir);

        var suffix = attempt.HasValue ? $"-{attempt.Value}" : "";
        return Path.Combine(runDir, $"{taskId}-{agent}{suffix}.log");
    }

    public static RunState NewRunState(string sprint, string branch)
    {
        var now = DateTime.UtcNow;
        var runId = now.ToString("yyyyMMddHHmmss");

        return new RunState
        {
            RunId = runId,
            Sprint = sprint,
            Branch = branch,
            StartedAt = now.ToString("O"),
        };
    }

    public static TaskState EnsureTaskState(RunState state, string taskId)
    {
        if (!state.Tasks.TryGetValue(taskId, out var ts))
        {
            ts = new TaskState { Id = taskId };
            state.Tasks[taskId] = ts;
        }
        return ts;
    }

    public static bool IsTaskDone(RunState state, string taskId) =>
        state.Tasks.TryGetValue(taskId, out var ts) && ts.Status == TaskStatus.Done;

    /// <summary>
    /// Returns true for tasks that should not be retried on resume: Done or Failed.
    /// Failed tasks need human intervention before they can be re-attempted.
    /// </summary>
    public static bool IsTaskTerminal(RunState state, string taskId) =>
        state.Tasks.TryGetValue(taskId, out var ts) &&
        ts.Status is TaskStatus.Done or TaskStatus.Failed;
}
