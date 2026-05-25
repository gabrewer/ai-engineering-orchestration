using PiLoop.Models;
using Spectre.Console;

namespace PiLoop.Services;

public sealed class BuildLoop
{
    private readonly DirectoryInfo _targetRoot;
    private readonly PiRuntimeOptions _piRuntime;
    private readonly bool _publishGitHub;
    private readonly PiWorkerRegistry _workerRegistry;
    private readonly SkillModelConfigService _skillModelConfig;
    private readonly PiWorkerContractBuilder _contractBuilder = new();
    private readonly PiResultValidator _resultValidator = new();
    private readonly TempLogService _tempLogs = new();

    public BuildLoop(DirectoryInfo targetRoot, PiRuntimeOptions? piRuntime = null, bool publishGitHub = true)
    {
        _targetRoot = targetRoot;
        _piRuntime = piRuntime ?? PiRuntimeOptions.From();
        _publishGitHub = publishGitHub;
        StateManager.SetRepoRoot(targetRoot.FullName);
        _workerRegistry = new PiWorkerRegistry(targetRoot.FullName);
        _skillModelConfig = new SkillModelConfigService(targetRoot);
    }

    public async Task ExecuteAsync(string sprintNameOrPath, string? branch = null, bool commit = true, bool resume = false, bool useWorktree = true)
    {
        StateManager.EnsureDotDir();
        var prd = await PrdReader.ReadAsync(sprintNameOrPath);

        if (useWorktree)
        {
            await ExecuteInWorktreeAsync(sprintNameOrPath, prd, branch, commit, resume);
            return;
        }

        var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        ActivityLog.Init(runId);
        ActivityLog.Info($"Build loop started for {prd.Sprint}");

        var existingState = resume ? await StateManager.LoadStateAsync(prd.Sprint) : null;
        var git = new GitService(_targetRoot.FullName);
        var requestedBranch = branch ?? existingState?.Branch ?? $"feature/{prd.Sprint}";
        var targetBranch = await git.EnsureBranchAsync(requestedBranch, prd.Sprint);
        EnsureNotProtectedBranch(targetBranch);
        var state = existingState ?? StateManager.NewRunState(prd.Sprint, targetBranch);
        state.Branch = targetBranch;
        await StateManager.SaveStateAsync(state);

        AnsiConsole.MarkupLine("[rgb(99,102,241)]⬡[/]  [bold]Build Loop[/]");
        AnsiConsole.MarkupLine($"[dim]  Target root: {Markup.Escape(_targetRoot.FullName)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Sprint: {Markup.Escape(prd.Sprint)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Branch: {Markup.Escape(targetBranch)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Pi: {Markup.Escape(_piRuntime.PiCommand)}[/]");
        if (resume && existingState is not null)
            AnsiConsole.MarkupLine($"[dim]  Resuming run state: {Markup.Escape(existingState.RunId)}[/]");
        Console.WriteLine();

        if (_publishGitHub)
        {
            await EnsureGitHubIssuesAsync(prd, state, targetBranch, reuseExistingState: resume);
        }

        foreach (var task in prd.Tasks)
        {
            if (StateManager.IsTaskDone(state, task.Id))
            {
                AnsiConsole.MarkupLine($"[dim]  Skipping {Markup.Escape(task.Id)} — already done.[/]");
                continue;
            }

            try
            {
                await RunTaskAsync(prd, state, task, runId, commit);
            }
            catch (Exception ex)
            {
                var taskState = StateManager.EnsureTaskState(state, task.Id);
                taskState.Status = Models.TaskStatus.Failed;
                taskState.LastFeedback = ex.Message;
                taskState.CompletedAt = DateTime.UtcNow.ToString("O");
                await StateManager.SaveStateAsync(state);
                throw;
            }
        }

        await RunSprintValidationAsync(state);

        Console.WriteLine();
        AnsiConsole.MarkupLine("[green]  ✓[/]  Build loop complete.");
    }

    private async Task ExecuteInWorktreeAsync(string sprintNameOrPath, Prd prd, string? branch, bool commit, bool resume)
    {
        if (resume)
            AnsiConsole.MarkupLine("[yellow]  --resume uses a fresh worktree unless --no-worktree is specified; existing completed state is not shared across worktrees.[/]");

        var mainGit = new GitService(_targetRoot.FullName);
        var baseBranch = await mainGit.GetCurrentBranchAsync();
        var targetBranch = branch ?? $"feature/{prd.Sprint}";
        EnsureNotProtectedBranch(targetBranch);

        var worktreeName = $"piloop-{SanitizeForPath(prd.Sprint)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        AnsiConsole.MarkupLine($"[rgb(99,102,241)]⬡[/]  [bold]Creating build worktree[/]");
        AnsiConsole.MarkupLine($"[dim]  Base: {Markup.Escape(baseBranch)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Branch: {Markup.Escape(targetBranch)}[/]");
        var worktreeGit = await mainGit.CreateWorktreeAsync(worktreeName, baseBranch);
        AnsiConsole.MarkupLine($"[dim]  Worktree: {Markup.Escape(worktreeGit.WorkDir)}[/]");
        Console.WriteLine();

        var worktreeLoop = new BuildLoop(new DirectoryInfo(worktreeGit.WorkDir), _piRuntime, _publishGitHub);
        await worktreeLoop.ExecuteAsync(sprintNameOrPath, targetBranch, commit, resume: false, useWorktree: false);
    }

    private static string SanitizeForPath(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static void EnsureNotProtectedBranch(string branch)
    {
        if (branch is "main" or "master")
            throw new InvalidOperationException("PiLoop build mode will not commit directly to main/master. Use a feature branch, e.g. --branch feature/<sprint>.");
    }

    private async Task EnsureGitHubIssuesAsync(Prd prd, RunState state, string branch, bool reuseExistingState)
    {
        if (reuseExistingState && state.Issues.EpicIssue is not null && state.Issues.TaskIssues.Count > 0)
            return;

        var github = new GitHubService(_targetRoot.FullName);
        var existing = state.Issues.EpicIssue is not null ? state.Issues : await github.FindExistingSprintIssuesAsync(prd.Sprint);
        var map = await github.CreateSprintIssuesAsync(prd, branch, existing: existing, createMissingTaskIssues: true);
        state.Issues = map;
        await StateManager.SaveStateAsync(state);
    }

    private async Task RunTaskAsync(Prd sprint, RunState state, PrdTask task, string runId, bool commit)
    {
        var taskState = StateManager.EnsureTaskState(state, task.Id);
        taskState.Status = Models.TaskStatus.Building;
        taskState.StartedAt = DateTime.UtcNow.ToString("O");
        await StateManager.SaveStateAsync(state);
        ActivityLog.TaskStart(task.Id, task.Title);

        var issueNumber = state.Issues.TaskIssues.GetValueOrDefault(task.Id);
        var audit = _publishGitHub ? new GitHubAuditService(_targetRoot.FullName) : null;
        if (audit is not null && issueNumber > 0)
        {
            await audit.PublishEvidenceToTaskAsync(issueNumber, new EvidenceEvent(
                EvidenceTarget.Task,
                EvidenceStatus.InProgress,
                "piloop",
                "Task started",
                $"Implement {task.Id}: {task.Title}.",
                "Run the appropriate Pi workers, validate generated changes, record evidence, and commit the completed task.",
                [], [], [], [], [], [], "Task execution started.", task.Id));
        }

        var before = await WorktreeChangeTracker.CaptureAsync(_targetRoot.FullName);
        var workerResults = new List<PiWorkerResult>();

        if (!task.SkipTests)
            workerResults.Add(await RunWorkerAsync(runId, task, "test-writer", BuildTaskPrompt(sprint, task, "test-writer")));

        foreach (var builder in GetBuilders(task.Type))
            workerResults.Add(await RunWorkerAsync(runId, task, builder, BuildTaskPrompt(sprint, task, builder)));

        var after = await WorktreeChangeTracker.CaptureAsync(_targetRoot.FullName);
        var changes = before.Diff(after);
        var validation = await RunValidationAsync();

        var gitCommit = commit && changes.Count > 0
            ? await CommitTaskAsync(task)
            : null;

        taskState.Status = validation.Passed ? Models.TaskStatus.Done : Models.TaskStatus.Failed;
        taskState.CompletedAt = DateTime.UtcNow.ToString("O");
        taskState.LastFeedback = validation.Summary;
        await StateManager.SaveStateAsync(state);

        if (audit is not null && issueNumber > 0)
        {
            var evidence = BuildTaskEvidence(task, state.Branch, gitCommit, workerResults, changes, validation);
            await audit.PublishEvidenceToTaskAsync(issueNumber, evidence);
        }

        ActivityLog.TaskDone(task.Id, taskState.Status);
    }

    private async Task<PiWorkerResult> RunWorkerAsync(string runId, PrdTask task, string workerName, string taskInput)
    {
        var worker = _workerRegistry.Get(workerName);
        var prompt = await _contractBuilder.BuildAsync(worker.PromptPath, taskInput);
        var workerRuntime = await ResolveWorkerRuntimeAsync(workerName, worker.PromptPath);
        var rpcRunner = new PiRpcRunner(workerRuntime);
        ActivityLog.AgentStart(task.Id, workerName, 1);
        var runResult = await rpcRunner.RunPromptAsync(_targetRoot.FullName, prompt, worker.Timeout);
        var logPath = _tempLogs.GetWorkerLogPath(runId, task.Id, workerName);
        await _tempLogs.WriteWorkerLogAsync(logPath, runResult.RawLines, runResult.DiagnosticSummary);
        var result = _resultValidator.ValidateRequired(runResult, workerName);
        ActivityLog.AgentDone(task.Id, workerName, 1, result.Status is PiWorkerStatus.Success, result.Summary);

        if (result.Status is PiWorkerStatus.Blocked or PiWorkerStatus.Failed or PiWorkerStatus.Escalate)
            throw new InvalidOperationException($"{workerName} returned {result.Status}: {result.Summary}");

        return result;
    }

    private async Task<PiRuntimeOptions> ResolveWorkerRuntimeAsync(string workerName, string promptPath)
    {
        var runtime = _piRuntime;

        var skillModel = await _skillModelConfig.FindAsync(workerName);
        if (skillModel is not null)
        {
            runtime = runtime with
            {
                Provider = string.IsNullOrWhiteSpace(runtime.Provider) ? skillModel.Provider : runtime.Provider,
                Model = string.IsNullOrWhiteSpace(runtime.Model) ? skillModel.Model : runtime.Model,
                Thinking = string.IsNullOrWhiteSpace(runtime.Thinking) ? skillModel.ThinkingLevel : runtime.Thinking,
            };
        }

        if (string.IsNullOrWhiteSpace(runtime.Model))
        {
            var metadata = await PiPromptMetadataReader.ReadAsync(promptPath);
            if (!string.IsNullOrWhiteSpace(metadata.Model))
                runtime = runtime with { Model = metadata.Model };
        }

        return runtime;
    }

    private string BuildTaskPrompt(Prd sprint, PrdTask task, string workerName)
    {
        var criteria = task.AcceptanceCriteria.Length == 0
            ? "- None supplied. Infer practical checks from the task description."
            : string.Join('\n', task.AcceptanceCriteria.Select(c => $"- {c}"));

        var mission = workerName switch
        {
            "test-writer" => "Create or update focused tests or validation documentation for this task before implementation.",
            "frontend-builder" => "Implement frontend/UI behavior for this task.",
            "backend-builder" => "Implement backend/domain/infrastructure behavior for this task.",
            _ => "Implement this task.",
        };

        return $"""
# Sprint: {sprint.Sprint}

{sprint.Description}

# Task: {task.Id} — {task.Title}

{task.Description}

## Acceptance Criteria
{criteria}

## Your Mission
{mission}

## Evidence Requirements
Your final JSON must make the work defensible. Include what you changed, why, alternatives considered, blockers, remaining issues, validation performed, and artifacts.

## Rules
- Modify files in the target repository; do not only describe a plan.
- Keep changes bounded to this task.
- Prefer simple, maintainable implementation.
- If the repo lacks an app scaffold, create the smallest structure needed for the task and document how to continue.
""";
    }

    private async Task<ValidationResult> RunValidationAsync()
    {
        var commands = new List<(string command, string args)>();
        var sln = Directory.GetFiles(_targetRoot.FullName, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln is not null)
        {
            commands.Add(("dotnet", $"build \"{sln}\" --no-restore -verbosity:quiet"));
            commands.Add(("dotnet", $"test \"{sln}\" --no-build --no-restore -verbosity:quiet"));
        }
        else if (File.Exists(Path.Combine(_targetRoot.FullName, "package.json")))
        {
            commands.Add(("npm", "test -- --runInBand"));
        }

        if (commands.Count == 0)
            return new ValidationResult(true, "No automatic build/test command detected; validation skipped.", []);

        var testResults = new List<EvidenceTestResult>();
        var passed = true;
        foreach (var (command, args) in commands)
        {
            var result = await RunProcessAsync(command, args);
            passed &= result.exitCode == 0;
            testResults.Add(new EvidenceTestResult($"{command} {args}", result.exitCode == 0 ? TestResultStatus.Passed : TestResultStatus.Failed, result.output));
        }

        return new ValidationResult(passed, passed ? "Validation passed." : "Validation failed.", testResults.ToArray());
    }

    private async Task RunSprintValidationAsync(RunState state)
    {
        var validation = await RunValidationAsync();
        ActivityLog.SmokeTest(validation.Passed, validation.Summary);
        state.Phases.SmokeTest = validation.Passed ? SprintPhaseStatus.Done : SprintPhaseStatus.Failed;
        await StateManager.SaveStateAsync(state);
    }

    private async Task<(int exitCode, string output)> RunProcessAsync(string command, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(command, args)
            {
                WorkingDirectory = _targetRoot.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var output = (stdout + "\n" + stderr).Trim();
            if (output.Length > 4000) output = output[..4000] + "\n... truncated ...";
            return (proc.ExitCode, string.IsNullOrWhiteSpace(output) ? $"{command} exited {proc.ExitCode}" : output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private async Task<GitCommitInfo> CommitTaskAsync(PrdTask task)
    {
        var git = new GitService(_targetRoot.FullName);
        await git.RunAsync("add", ".");
        await git.RunAsync("commit", "-m", $"{task.Id}: {task.Title}");
        return new GitCommitInfo(await git.GetHeadShaAsync(), await git.GetShortHeadShaAsync());
    }

    private static string[] GetBuilders(TaskType type) => type switch
    {
        TaskType.Backend => ["backend-builder"],
        TaskType.Frontend => ["frontend-builder"],
        TaskType.Both => ["backend-builder", "frontend-builder"],
        _ => ["backend-builder"],
    };

    private static EvidenceEvent BuildTaskEvidence(PrdTask task, string branch, GitCommitInfo? gitCommit, IReadOnlyList<PiWorkerResult> results, IReadOnlyList<WorktreeChange> changes, ValidationResult validation)
    {
        var work = results.Select(r => r.WhatHappened)
            .Concat([$"Branch: {branch}"])
            .Concat(gitCommit is null ? [] : [$"Commit: {gitCommit.ShortSha} ({gitCommit.Sha})"])
            .Concat(changes.Select(c => $"{c.Status.Trim()} {c.Path}"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var decisions = results.Select(r => new EvidenceDecision(
            r.Summary,
            r.Why,
            r.AlternativesConsidered,
            "Captured from worker final result; no explicit tradeoff field was provided."))
            .ToArray();

        var blockers = results
            .Where(r => r.Status is PiWorkerStatus.Blocked or PiWorkerStatus.Escalate or PiWorkerStatus.Failed)
            .Select(r => new EvidenceBlocker(r.Summary, r.Status.ToString(), r.NextAction))
            .ToArray();

        var artifacts = results.SelectMany(r => r.Artifacts.Select(a => a.Path))
            .Concat(changes.Select(c => c.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new EvidenceEvent(
            EvidenceTarget.Task,
            validation.Passed ? EvidenceStatus.Verified : EvidenceStatus.Failed,
            "piloop-build",
            "Task implementation completed",
            $"Implement {task.Id}: {task.Title}.",
            gitCommit is null
                ? "Run Pi workers for tests and implementation, validate the repository, and record the resulting changes. No commit was created for this task."
                : $"Run Pi workers for tests and implementation, validate the repository, and commit the resulting changes on branch {branch} at {gitCommit.ShortSha}.",
            work,
            decisions,
            blockers,
            validation.TestResults,
            validation.Passed ? [] : [new EvidenceRemainingIssue("Validation failed", "Build loop does not auto-fix validation failures yet.", "Inspect logs and rerun after fixing failures.")],
            artifacts,
            validation.Summary,
            task.Id);
    }

    private sealed record ValidationResult(bool Passed, string Summary, EvidenceTestResult[] TestResults);
    private sealed record GitCommitInfo(string Sha, string ShortSha);
}
