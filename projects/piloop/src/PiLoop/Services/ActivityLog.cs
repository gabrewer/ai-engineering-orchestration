using PiLoop.Models;

namespace PiLoop.Services;

/// <summary>
/// Appends timestamped entries to a single human-readable log file for the run.
/// Written to .piloop/activity.log — one file, always fresh per run, easy to tail.
/// </summary>
public static class ActivityLog
{
    private static string? _path;
    private static readonly object _lock = new();

    public static void Init(string runId)
    {
        var dir = Path.Combine(StateManager.DotDir, "logs", runId);
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "activity.log");

        // Start fresh for this run
        File.WriteAllText(_path, $"[{Ts()}] ═══ PiLoop started — run {runId} ═══\n");
    }

    public static void Sprint(string sprint, string branch) =>
        Write($"SPRINT  {sprint}  branch={branch}");

    public static void Phase(string phase) =>
        Write($"PHASE   {phase}");

    public static void TaskStart(string taskId, string title) =>
        Write($"TASK    {taskId} started  — {title}");

    public static void AgentStart(string taskId, string agent, int attempt) =>
        Write(attempt > 1
            ? $"AGENT   {taskId}/{agent} attempt {attempt}"
            : $"AGENT   {taskId}/{agent}");

    public static void AgentDone(string taskId, string agent, int attempt, bool ok, string? note = null) =>
        Write(ok
            ? $"OK      {taskId}/{agent}{(attempt > 1 ? $" attempt {attempt}" : "")} {note ?? ""}"
            : $"FAIL    {taskId}/{agent}{(attempt > 1 ? $" attempt {attempt}" : "")} {note ?? ""}");

    public static void AgentTimeout(string taskId, string agent, int timeoutMinutes, int retryNum) =>
        Write($"TIMEOUT {taskId}/{agent} exceeded {timeoutMinutes}m — restarting ({retryNum}/3)");

    public static void AgentFailed(string taskId, string agent, string reason) =>
        Write($"FAIL    {taskId}/{agent} {reason}");

    public static void AgentProgress(string taskId, string agent, TimeSpan elapsed, TimeSpan idle, long bytesWritten, string status) =>
        Write($"PROGRESS {taskId}/{agent} elapsed={FormatDuration(elapsed)} idle={FormatDuration(idle)} log={FormatBytes(bytesWritten)} status={status}");

    public static void ChangeGate(string taskId, string agent, bool ok, string? note = null) =>
        Write(ok
            ? $"CHANGES {taskId}/{agent} OK {note ?? ""}"
            : $"CHANGES {taskId}/{agent} FAILED — {note}");

    public static void BuildGate(string taskId, bool ok, string? error = null) =>
        Write(ok
            ? $"BUILD   {taskId} OK"
            : $"BUILD   {taskId} FAILED — {error?.Split('\n')[0]}");

    public static void TaskDone(string taskId, Models.TaskStatus status) =>
        Write(status == Models.TaskStatus.Done
            ? $"DONE    {taskId}"
            : $"FAILED  {taskId} status={status}");

    public static void SmokeTest(bool ok, string? note = null) =>
        Write(ok ? $"SMOKE   OK {note}" : $"SMOKE   FAILED — {note}");

    public static void Info(string message) =>
        Write($"INFO    {message}");

    public static void Error(string message) =>
        Write($"ERROR   {message}");

    private static string Ts() => DateTime.UtcNow.ToString("HH:mm:ss");

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h{duration.Minutes:D2}m";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m{duration.Seconds:D2}s";
        return $"{Math.Max(0, (int)duration.TotalSeconds)}s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024 * 1024)}MB";
        if (bytes >= 1024)
            return $"{bytes / 1024}KB";
        return $"{bytes}B";
    }

    private static void Write(string line)
    {
        if (_path is null) return;
        var entry = $"[{Ts()}] {line}\n";
        lock (_lock)
        {
            try { File.AppendAllText(_path, entry); }
            catch { /* never crash the pipeline over logging */ }
        }
    }
}
