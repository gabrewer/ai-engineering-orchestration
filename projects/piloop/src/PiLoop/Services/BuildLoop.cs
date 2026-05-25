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

        await RunSprintPhasesAsync(prd, state, runId);

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

    private async Task RunSprintPhasesAsync(Prd prd, RunState state, string runId)
    {
        var phases = prd.EffectivePhases;

        if (phases.DomainModeling && state.Phases.DomainModeling != SprintPhaseStatus.Done)
        {
            state.Phases.DomainModeling = SprintPhaseStatus.Running;
            await StateManager.SaveStateAsync(state);
            try
            {
                await RunSprintWorkerAsync(runId, prd, "domain-modeler", BuildDomainModelPrompt(prd));
                state.Phases.DomainModeling = SprintPhaseStatus.Done;
            }
            catch
            {
                state.Phases.DomainModeling = SprintPhaseStatus.Failed;
                await StateManager.SaveStateAsync(state);
                throw;
            }
            await StateManager.SaveStateAsync(state);
        }
        else if (!phases.DomainModeling)
        {
            state.Phases.DomainModeling = SprintPhaseStatus.Done;
            await StateManager.SaveStateAsync(state);
        }

        if (phases.ApiContract && state.Phases.ApiContract != SprintPhaseStatus.Done)
        {
            state.Phases.ApiContract = SprintPhaseStatus.Running;
            await StateManager.SaveStateAsync(state);
            try
            {
                await RunSprintWorkerAsync(runId, prd, "api-developer", BuildApiContractPrompt(prd));
                state.Phases.ApiContract = SprintPhaseStatus.Done;
            }
            catch
            {
                state.Phases.ApiContract = SprintPhaseStatus.Failed;
                await StateManager.SaveStateAsync(state);
                throw;
            }
            await StateManager.SaveStateAsync(state);
        }
        else if (!phases.ApiContract)
        {
            state.Phases.ApiContract = SprintPhaseStatus.Done;
            await StateManager.SaveStateAsync(state);
        }
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

        var validation = await RunValidationAsync();
        if (validation.Passed)
        {
            workerResults.Add(await RunWorkerAsync(runId, task, "destroyer", BuildTaskPrompt(sprint, task, "destroyer")));
            var review = await RunReviewLoopAsync(sprint, task, runId, workerResults);
            workerResults.AddRange(review.Results);
            validation = review.Validation;
        }

        var after = await WorktreeChangeTracker.CaptureAsync(_targetRoot.FullName);
        var changes = before.Diff(after);

        var gitCommit = commit && validation.Passed && changes.Count > 0
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

    private async Task<PiWorkerResult> RunSprintWorkerAsync(string runId, Prd prd, string workerName, string taskInput)
    {
        var task = new PrdTask($"sprint-{workerName}", $"{workerName} — {prd.Sprint}", TaskType.Both, taskInput, []);
        var result = await RunWorkerAsync(runId, task, workerName, taskInput);

        var audit = _publishGitHub && File.Exists(Path.Combine(_targetRoot.FullName, ".git"))
            ? new GitHubAuditService(_targetRoot.FullName)
            : null;
        var state = await StateManager.LoadStateAsync(prd.Sprint);
        if (audit is not null && state?.Issues.EpicIssue is { } epic)
        {
            await audit.PublishEvidenceToEpicAsync(epic, EvidenceRenderer.FromWorkerResult(
                result,
                EvidenceTarget.Epic,
                EvidenceStatus.Completed,
                workerName,
                workerName == "domain-modeler" ? "Domain model completed" : "API contract completed",
                workerName == "domain-modeler"
                    ? "Define or update the sprint domain model before task implementation."
                    : "Define or update the sprint API contract before task implementation.",
                "Run the sprint-level Pi worker, write durable design artifacts, and record the rationale."));
        }

        return result;
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
            "destroyer" => "Adversarially inspect the task implementation. Try to break it, run focused checks, and report concrete findings. Do not make broad unrelated changes.",
            "review-agent" => "Review the implementation and destroyer findings. Return success only if the task is defensible; return changes_needed with concrete feedback if fixes are required.",
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

    private static string BuildDomainModelPrompt(Prd prd)
    {
        var taskSummary = string.Join('\n', prd.Tasks.Select(t =>
            $"- {t.Id}: {t.Title} ({t.Type})\n  {t.Description}"));

        return $"""
# Sprint: {prd.Sprint}

{prd.Description}

## Tasks in This Sprint

{taskSummary}

## Your Mission

Define or update the domain model for this sprint's scope.

Create `docs/domain/` if it does not exist.
Output your domain model to `docs/domain/{prd.Sprint}.md`.

For each domain concept touched by these tasks, define:
- entities or aggregates
- value objects
- commands or operations
- state transitions
- invariants and validation rules
- assumptions and out-of-scope behavior

## Evidence Requirements
Your final JSON must make the work defensible. Include what you changed, why, alternatives considered, blockers, remaining issues, validation performed, and artifacts.
""";
    }

    private static string BuildApiContractPrompt(Prd prd)
    {
        var taskSummary = string.Join('\n', prd.Tasks.Select(t =>
            $"- {t.Id}: {t.Title} ({t.Type})\n  {t.Description}"));

        return $"""
# Sprint: {prd.Sprint}

{prd.Description}

## Tasks in This Sprint

{taskSummary}

## Your Mission

Define or update the API or interface contract for this sprint's scope.

Create `docs/api/` if it does not exist.
Output your contract to `docs/api/{prd.Sprint}.md`.

If this project does not expose HTTP APIs yet, document the internal module/interface contract needed by the tasks instead.

For each operation needed by these tasks, define:
- name and purpose
- input shape
- output shape
- validation/errors
- owning module or layer
- assumptions and out-of-scope behavior

Read `docs/domain/{prd.Sprint}.md` first if it exists.

## Evidence Requirements
Your final JSON must make the work defensible. Include what you changed, why, alternatives considered, blockers, remaining issues, validation performed, and artifacts.
""";
    }

    private async Task<ReviewLoopResult> RunReviewLoopAsync(Prd sprint, PrdTask task, string runId, List<PiWorkerResult> priorResults)
    {
        var results = new List<PiWorkerResult>();
        var validation = await RunValidationAsync();
        if (!validation.Passed)
            return new ReviewLoopResult(results, validation);

        var feedback = BuildReviewContext(priorResults);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var reviewResult = await RunWorkerAsync(runId, task, "review-agent", BuildTaskPrompt(sprint, task, "review-agent") + $"\n\n## Review Context\n{feedback}");
            results.Add(reviewResult);

            if (reviewResult.Status is PiWorkerStatus.Success)
                return new ReviewLoopResult(results, validation);

            if (reviewResult.Status is PiWorkerStatus.Blocked or PiWorkerStatus.Failed or PiWorkerStatus.Escalate)
                return new ReviewLoopResult(results, validation with { Passed = false, Summary = $"Review failed: {reviewResult.Summary}" });

            if (reviewResult.Status is not PiWorkerStatus.ChangesNeeded || attempt == 3)
                return new ReviewLoopResult(results, validation with { Passed = false, Summary = $"Review did not approve after {attempt} attempt(s): {reviewResult.Summary}" });

            feedback = reviewResult.WhatHappened + "\n\n" + reviewResult.NextAction;
            foreach (var builder in GetBuilders(task.Type))
                results.Add(await RunWorkerAsync(runId, task, builder, BuildTaskPrompt(sprint, task, builder) + $"\n\n## Review Feedback To Fix\n{feedback}"));

            validation = await RunValidationAsync();
            if (!validation.Passed)
                return new ReviewLoopResult(results, validation);
        }

        return new ReviewLoopResult(results, validation with { Passed = false, Summary = "Review loop exhausted." });
    }

    private static string BuildReviewContext(IEnumerable<PiWorkerResult> results) =>
        string.Join("\n\n", results.Select(r => $"## {r.Status}: {r.Summary}\n{r.WhatHappened}\nWhy: {r.Why}\nNext: {r.NextAction}"));

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
    private sealed record ReviewLoopResult(List<PiWorkerResult> Results, ValidationResult Validation);
    private sealed record GitCommitInfo(string Sha, string ShortSha);
}
