using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PiLoop.Models;
using Spectre.Console;

namespace PiLoop.Services;

/// <summary>
/// Wraps the gh CLI for creating and updating GitHub issues.
/// </summary>
public sealed class GitHubService
{
    private readonly string _workDir;
    private readonly HashSet<string> _ensuredLabels = new();
    private string? _repoName;

    private static readonly Dictionary<string, string> RequiredLabels = new()
    {
        ["epic"] = "0e8a16",
        ["task"] = "1d76db",
        ["piloop"] = "5319e7",
    };

    public GitHubService(string? workDir = null)
    {
        _workDir = workDir ?? Directory.GetCurrentDirectory();
    }

    /// <summary>Returns "owner/repo" for the current working directory.</summary>
    private async Task<string> GetRepoNameAsync()
    {
        _repoName ??= (await RunGhAsync("repo", "view", "--json", "nameWithOwner", "-q", ".nameWithOwner")).Trim();
        return _repoName;
    }

    /// <summary>Returns the GraphQL node ID of an issue.</summary>
    private async Task<string> GetIssueNodeIdAsync(int issueNumber)
    {
        var repo = await GetRepoNameAsync();
        return (await RunGhAsync("api", $"repos/{repo}/issues/{issueNumber}", "--jq", ".node_id")).Trim();
    }

    /// <summary>
    /// Adds taskNodeId as a sub-issue of epicNodeId via GraphQL.
    /// Swallows failures so sub-issue errors never block the pipeline.
    /// </summary>
    private async Task AddSubIssueSafeAsync(string epicNodeId, string taskNodeId)
    {
        try
        {
            var mutation = $"mutation {{ addSubIssue(input: {{issueId: \"{epicNodeId}\", subIssueId: \"{taskNodeId}\"}}) {{ issue {{ number }} subIssue {{ number }} }} }}";
            await RunGhAsync("api", "graphql", "-f", $"query={mutation}");
        }
        catch { /* sub-issues not supported or network error — fall back to body links */ }
    }

    /// <summary>
    /// Ensures a label exists on the repo. Creates it if missing. Idempotent per instance.
    /// </summary>
    private async Task EnsureLabelAsync(string name)
    {
        if (!_ensuredLabels.Add(name)) return;

        var color = RequiredLabels.GetValueOrDefault(name, "ededed");
        try
        {
            await RunGhAsync("label", "create", name, "--color", color, "--force");
        }
        catch
        {
            // --force means "update if exists", so this should only fail on auth/network issues.
            // Swallow and let issue creation fail with a clearer error if the label is truly missing.
        }
    }

    /// <summary>
    /// Ensures all labels in the list exist before creating issues.
    /// </summary>
    private async Task EnsureLabelsAsync(string[] labels)
    {
        foreach (var label in labels)
            await EnsureLabelAsync(label);
    }

    /// <summary>
    /// Finds an existing epic issue for a sprint by searching for "[Sprint] {name}" in open issues.
    /// Returns the IssueMap if found (epic + child issues parsed from body), or null.
    /// </summary>
    public async Task<IssueMap?> FindExistingSprintIssuesAsync(string sprintName)
    {
        try
        {
            var output = await RunGhAsync("issue", "list",
                "--label", "epic",
                "--search", $"[Sprint] {sprintName} in:title",
                "--json", "number,title,body",
                "--limit", "1");

            using var doc = JsonDocument.Parse(output);
            var issues = doc.RootElement;
            if (issues.GetArrayLength() == 0) return null;

            var epic = issues[0];
            var epicNumber = epic.GetProperty("number").GetInt32();
            var body = epic.GetProperty("body").GetString() ?? "";

            // Parse task issue numbers from epic body: "- [ ] **TASK-001**: ... (#42)"
            var taskIssues = new Dictionary<string, int>();
            foreach (var line in body.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("- [")) continue;

                // Extract task ID: **TASK-001**
                var boldStart = trimmed.IndexOf("**");
                var boldEnd = trimmed.IndexOf("**", boldStart + 2);
                if (boldStart < 0 || boldEnd < 0) continue;
                var taskId = trimmed[(boldStart + 2)..boldEnd];

                // Extract issue number: (#42)
                var hashStart = trimmed.LastIndexOf("(#");
                var hashEnd = trimmed.LastIndexOf(')');
                if (hashStart < 0 || hashEnd < 0) continue;
                var numStr = trimmed[(hashStart + 2)..hashEnd];
                if (int.TryParse(numStr, out var num))
                    taskIssues[taskId] = num;
            }

            return new IssueMap { EpicIssue = epicNumber, TaskIssues = taskIssues };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates the epic (parent) issue for a sprint and all child task issues.
    /// Returns a mapping of task IDs → issue numbers.
    /// </summary>
    public async Task<IssueMap> CreateSprintIssuesAsync(Prd prd, string branch,
        Func<IssueMap, Task>? onProgress = null,
        IssueMap? existing = null,
        bool createMissingTaskIssues = true)
    {
        // Ensure all required labels exist
        await EnsureLabelsAsync(["epic", "task", "piloop"]);

        int epicNumber;
        if (existing?.EpicIssue.HasValue == true)
        {
            // Resume: re-use the existing epic
            epicNumber = existing.EpicIssue.Value;
        }
        else
        {
            // Build epic body with task list
            var body = new StringBuilder();
            body.AppendLine($"Sprint: **{prd.Sprint}**");
            body.AppendLine($"Branch: `{branch}`");
            body.AppendLine();
            body.AppendLine(prd.Description);
            body.AppendLine();
            body.AppendLine("## Tasks");
            body.AppendLine();

            foreach (var task in prd.Tasks)
                body.AppendLine($"- [ ] **{task.Id}**: {task.Title}");

            epicNumber = await CreateIssueAsync(
                $"[Sprint] {prd.Sprint}",
                body.ToString(),
                ["epic", "piloop"]);

            AnsiConsole.MarkupLine($"[dim]  Created epic issue #{epicNumber}[/]");

            // Save progress after epic so resume can skip re-creating it
            if (onProgress is not null)
                await onProgress(new IssueMap { EpicIssue = epicNumber, TaskIssues = new() });
        }

        // Get the epic's node ID for sub-issue linking — failure is non-fatal
        string? epicNodeId = null;
        try { epicNodeId = await GetIssueNodeIdAsync(epicNumber); }
        catch { /* sub-issue linking not available — fall back to body links only */ }

        // Create child issues for each task, linked as sub-issues
        var taskIssues = new Dictionary<string, int>(existing?.TaskIssues ?? new());
        foreach (var task in prd.Tasks)
        {
            // Skip tasks that already have issues (partial resume)
            if (taskIssues.ContainsKey(task.Id)) continue;

            if (!createMissingTaskIssues)
            {
                AnsiConsole.MarkupLine($"[yellow]  Skipped new issue for {Markup.Escape(task.Id)}; rerun with --allow-new-issues to create tasks added by replanning.[/]");
                continue;
            }

            var taskBody = new StringBuilder();
            taskBody.AppendLine($"Parent: #{epicNumber}");
            taskBody.AppendLine($"Type: `{task.Type}`");
            taskBody.AppendLine();
            taskBody.AppendLine(task.Description);

            if (task.AcceptanceCriteria is { Length: > 0 })
            {
                taskBody.AppendLine();
                taskBody.AppendLine("## Acceptance Criteria");
                taskBody.AppendLine();
                foreach (var ac in task.AcceptanceCriteria)
                    taskBody.AppendLine($"- [ ] {ac}");
            }

            var issueNumber = await CreateIssueAsync(
                $"{task.Id}: {task.Title}",
                taskBody.ToString(),
                ["task", "piloop"]);

            taskIssues[task.Id] = issueNumber;
            AnsiConsole.MarkupLine($"[dim]  Created issue #{issueNumber} for {task.Id}[/]");

            // Save progress after each task issue so resume can skip already-created ones
            if (onProgress is not null)
                await onProgress(new IssueMap { EpicIssue = epicNumber, TaskIssues = new(taskIssues) });

            // Link as a sub-issue of the epic if we have node IDs
            if (epicNodeId is not null)
            {
                try
                {
                    var taskNodeId = await GetIssueNodeIdAsync(issueNumber);
                    await AddSubIssueSafeAsync(epicNodeId, taskNodeId);
                }
                catch { /* sub-issue linking failed — body links are the fallback */ }
            }
        }

        // Update epic body with linked issue numbers
        var linkedBody = new StringBuilder();
        linkedBody.AppendLine($"Sprint: **{prd.Sprint}**");
        linkedBody.AppendLine($"Branch: `{branch}`");
        linkedBody.AppendLine();
        linkedBody.AppendLine(prd.Description);
        linkedBody.AppendLine();
        linkedBody.AppendLine("## Tasks");
        linkedBody.AppendLine();

        foreach (var task in prd.Tasks)
        {
            if (taskIssues.TryGetValue(task.Id, out var num))
                linkedBody.AppendLine($"- [ ] **{task.Id}**: {task.Title} (#{num})");
            else
                linkedBody.AppendLine($"- [ ] **{task.Id}**: {task.Title} _(issue not created; rerun with --allow-new-issues)_");
        }

        await RunGhAsync("issue", "edit", epicNumber.ToString(), "--body", linkedBody.ToString());

        return new IssueMap { EpicIssue = epicNumber, TaskIssues = taskIssues };
    }

    /// <summary>
    /// Updates the emoji status prefix on an issue title.
    /// </summary>
    public async Task UpdateIssueStatusAsync(int issueNumber, string emoji)
    {
        var currentTitle = await RunGhAsync("issue", "view", issueNumber.ToString(),
            "--json", "title", "-q", ".title");
        currentTitle = currentTitle.Trim();

        // Strip any existing emoji prefix (emoji + space at start)
        var clean = StripEmojiPrefix(currentTitle);
        var newTitle = string.IsNullOrEmpty(emoji) ? clean : $"{emoji} {clean}";

        await RunGhAsync("issue", "edit", issueNumber.ToString(), "--title", newTitle);
    }

    /// <summary>
    /// Adds a comment to an issue.
    /// </summary>
    public async Task CommentOnIssueAsync(int issueNumber, string body)
    {
        await RunGhAsync("issue", "comment", issueNumber.ToString(), "--body", body);
    }

    /// <summary>
    /// Checks a task checkbox on the epic issue body.
    /// </summary>
    public async Task CheckTaskOnEpicAsync(int epicNumber, string taskId)
    {
        var body = await RunGhAsync("issue", "view", epicNumber.ToString(),
            "--json", "body", "-q", ".body");

        var updated = body.Replace($"- [ ] **{taskId}**", $"- [x] **{taskId}**");
        if (updated != body)
            await RunGhAsync("issue", "edit", epicNumber.ToString(), "--body", updated);
    }

    private async Task<int> CreateIssueAsync(string title, string body, string[] labels)
    {
        var args = new List<string> { "issue", "create", "--title", title, "--body", body };
        foreach (var label in labels)
        {
            args.Add("--label");
            args.Add(label);
        }

        var output = await RunGhAsync(args.ToArray());
        // gh issue create outputs the URL: https://github.com/owner/repo/issues/123
        var url = output.Trim();
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..], out var number))
            return number;

        throw new InvalidOperationException($"Could not parse issue number from gh output: {url}");
    }

    private async Task<string> RunGhAsync(params string[] args)
    {
        // Retry up to 3 times for transient GitHub API errors (5xx, rate limits)
        var delays = new[] { 2_000, 5_000, 15_000 };
        string? lastError = null;

        for (var attempt = 0; attempt <= delays.Length; attempt++)
        {
            var psi = new ProcessStartInfo("gh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = _workDir,
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start gh process");

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0) return stdout;

            lastError = stderr.Trim();

            // Only retry on transient errors (5xx or rate limit)
            var isTransient = lastError.Contains("502") || lastError.Contains("503")
                || lastError.Contains("504") || lastError.Contains("rate limit");

            if (!isTransient || attempt == delays.Length)
                throw new InvalidOperationException(
                    $"gh {string.Join(' ', args)} failed (exit {proc.ExitCode}): {lastError}");

            await Task.Delay(delays[attempt]);
        }

        throw new InvalidOperationException($"gh command failed after retries: {lastError}");
    }

    /// <summary>
    /// Strips emoji prefix from an issue title. Handles multi-byte emoji followed by a space.
    /// </summary>
    internal static string StripEmojiPrefix(string title)
    {
        if (string.IsNullOrEmpty(title)) return title;

        // Check if the title starts with a known status emoji
        string[] emojis = ["🏃", "✋", "🔴", "🔵", "👀", "✅"];
        foreach (var emoji in emojis)
        {
            if (title.StartsWith(emoji))
            {
                var rest = title[emoji.Length..];
                return rest.StartsWith(' ') ? rest[1..] : rest;
            }
        }

        return title;
    }
}
