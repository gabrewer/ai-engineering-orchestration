using PiLoop.Models;
using Spectre.Console;

namespace PiLoop.Services;

/// <summary>
/// Pi-backed planning loop: Product Designer + PM create sprint briefs and sprint plans.
/// </summary>
public sealed class PlanningLoop
{
    private readonly DirectoryInfo _targetRoot;
    private readonly string _prdPath;
    private readonly string _runTimestamp;
    private readonly PiRuntimeOptions _piRuntime;
    private readonly bool _publishGitHub;
    private readonly bool _allowNewGitHubIssues;
    private readonly PiWorkerRegistry _workerRegistry;
    private readonly SkillModelConfigService _skillModelConfig;
    private readonly PiWorkerContractBuilder _contractBuilder;
    private readonly PiResultValidator _resultValidator;
    private readonly TempLogService _tempLogs;

    private const string AnswersPath = "docs/sprints/answers.md";

    public PlanningLoop(DirectoryInfo targetRoot, string prdPath, PiRuntimeOptions? piRuntime = null, bool publishGitHub = true, bool allowNewGitHubIssues = false)
    {
        _targetRoot = targetRoot;
        _prdPath = prdPath;
        _publishGitHub = publishGitHub;
        _allowNewGitHubIssues = allowNewGitHubIssues;
        StateManager.SetRepoRoot(targetRoot.FullName);
        _runTimestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        _piRuntime = piRuntime ?? PiRuntimeOptions.From();
        _workerRegistry = new PiWorkerRegistry(targetRoot.FullName);
        _skillModelConfig = new SkillModelConfigService(targetRoot);
        _contractBuilder = new PiWorkerContractBuilder();
        _resultValidator = new PiResultValidator();
        _tempLogs = new TempLogService();
    }

    private string QuestionsPath => $"docs/sprints/questions-{_runTimestamp}.md";
    private string BriefSuffix => $"-brief-{_runTimestamp}.md";
    private string PrdSuffix => $"-{_runTimestamp}.json";

    public async Task ExecuteAsync()
    {
        StateManager.EnsureDotDir();
        var runId = _runTimestamp.Replace("-", "");
        ActivityLog.Init(runId);
        ActivityLog.Info($"Planning loop started for {_prdPath}");

        var hasAnswers = File.Exists(Path.Combine(_targetRoot.FullName, AnswersPath));

        AnsiConsole.MarkupLine($"[rgb(99,102,241)]⬡[/]  [bold]Planning Loop[/]");
        AnsiConsole.MarkupLine($"[dim]  PRD: {Markup.Escape(_prdPath)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Run: {Markup.Escape(_runTimestamp)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Pi: {Markup.Escape(_piRuntime.PiCommand)}[/]");
        if (!string.IsNullOrWhiteSpace(_piRuntime.Provider))
            AnsiConsole.MarkupLine($"[dim]  Provider: {Markup.Escape(_piRuntime.Provider)}[/]");
        if (!string.IsNullOrWhiteSpace(_piRuntime.Model))
            AnsiConsole.MarkupLine($"[dim]  Model: {Markup.Escape(_piRuntime.Model)}[/]");
        if (!string.IsNullOrWhiteSpace(_piRuntime.Thinking))
            AnsiConsole.MarkupLine($"[dim]  Thinking: {Markup.Escape(_piRuntime.Thinking)}[/]");
        if (hasAnswers)
            AnsiConsole.MarkupLine($"[dim]  Answers: {Markup.Escape(AnswersPath)} (found)[/]");
        Console.WriteLine();

        AnsiConsole.MarkupLine("[bold]Step 1:[/] Product Designer — expanding all milestones");
        var designerInput = BuildDesignerPrompt(hasAnswers);
        var designerResult = await RunPlanningWorkerAsync(runId, "plan-design", "product-designer", designerInput);
        AnsiConsole.MarkupLine("[green]  ✓[/]  Sprint briefs written to [bold]docs/sprints/[/]");
        Console.WriteLine();

        AnsiConsole.MarkupLine("[bold]Step 2:[/] PM — breaking all milestones into sprint plans");
        var pmInput = BuildPmPrompt(hasAnswers);
        var pmResult = await RunPlanningWorkerAsync(runId, "plan-pm", "pm", pmInput);
        AnsiConsole.MarkupLine("[green]  ✓[/]  Sprint plans written to [bold]docs/sprints/[/]");

        var questionsFile = Path.Combine(_targetRoot.FullName, QuestionsPath);
        if (File.Exists(questionsFile))
        {
            var content = await File.ReadAllTextAsync(questionsFile);
            if (!string.IsNullOrWhiteSpace(content) && !content.Contains("NO QUESTIONS", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                AnsiConsole.MarkupLine("[yellow]  The agents have questions that need answers before the plan is complete.[/]");
                AnsiConsole.MarkupLine($"[yellow]  Questions: [bold]{Markup.Escape(QuestionsPath)}[/][/]");
                Console.WriteLine();
                AnsiConsole.MarkupLine("[dim]  1. Read the questions[/]");
                AnsiConsole.MarkupLine($"[dim]  2. Write your answers to [bold]{Markup.Escape(AnswersPath)}[/][/]");
                AnsiConsole.MarkupLine($"[dim]  3. Re-run: [bold]piloop plan --target-root {Markup.Escape(_targetRoot.FullName)} --prd {Markup.Escape(_prdPath)}[/][/]");
                Console.WriteLine();
                return;
            }
        }

        var manifestService = new PlanManifestService(_targetRoot);
        var manifest = await manifestService.LoadAsync();
        var prdHash = await manifestService.ComputeFileHashAsync(_prdPath);

        string[] sprintFiles;
        try
        {
            sprintFiles = PrdReader.DiscoverAllSprintPlans();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]  Warning: Could not discover sprint plans: {Markup.Escape(ex.Message)}[/]");
            sprintFiles = [];
        }

        foreach (var file in sprintFiles)
        {
            try
            {
                var prd = await PrdReader.ReadAsync(file);
                var logicalSprint = PrdReader.ExtractLogicalSprintName(file);
                var briefPath = manifestService.FindLatestBriefForLogicalSprint(logicalSprint);
                var existingEntry = manifestService.Find(manifest, prdHash, logicalSprint);
                manifestService.Upsert(
                    manifest,
                    _prdPath,
                    prdHash,
                    logicalSprint,
                    prd.Sprint,
                    briefPath,
                    file,
                    _runTimestamp,
                    existingEntry?.GitHubIssues);
            }
            catch (Exception ex)
            {
                var sprintName = PrdReader.ExtractLogicalSprintName(file);
                AnsiConsole.MarkupLine($"[yellow]  Warning: Failed to add {Markup.Escape(sprintName)} to manifest: {Markup.Escape(ex.Message)}[/]");
            }
        }
        await manifestService.SaveAsync(manifest);

        Console.WriteLine();
        AnsiConsole.MarkupLine($"[dim]  Manifest: {Markup.Escape(Path.GetRelativePath(_targetRoot.FullName, manifestService.ManifestPath))}[/]");

        if (!_publishGitHub)
        {
            AnsiConsole.MarkupLine("[yellow]Step 3:[/] Skipping GitHub issue creation (--skip-github).");
            Console.WriteLine();
            AnsiConsole.MarkupLine("[green]  ✓[/]  All sprint plans written to [bold]docs/sprints/[/]");
            return;
        }

        AnsiConsole.MarkupLine("[bold]Step 3:[/] Creating GitHub issues for each sprint");

        var github = new GitHubService(_targetRoot.FullName);
        var audit = new GitHubAuditService(_targetRoot.FullName);

        foreach (var file in sprintFiles)
        {
            try
            {
                var prd = await PrdReader.ReadAsync(file);
                var logicalSprint = PrdReader.ExtractLogicalSprintName(file);
                var branch = $"feature/{logicalSprint}";
                var manifestEntry = manifestService.Find(manifest, prdHash, logicalSprint);
                var existing = manifestEntry?.GitHubIssues ?? await github.FindExistingSprintIssuesAsync(prd.Sprint);
                var hadExistingIssues = existing?.EpicIssue is not null;
                var createMissingTaskIssues = !hadExistingIssues || _allowNewGitHubIssues;
                var map = await github.CreateSprintIssuesAsync(prd, branch, existing: existing, createMissingTaskIssues: createMissingTaskIssues);
                var briefPath = manifestService.FindLatestBriefForLogicalSprint(logicalSprint);
                manifestService.Upsert(manifest, _prdPath, prdHash, logicalSprint, prd.Sprint, briefPath, file, _runTimestamp, map);
                await manifestService.SaveAsync(manifest);
                var issueVerb = hadExistingIssues ? "reused" : "created";
                AnsiConsole.MarkupLine($"[green]  ✓[/]  {prd.Sprint}: {issueVerb} epic #{map.EpicIssue}, {map.TaskIssues.Count} task issues");

                if (map.EpicIssue is { } epic)
                {
                    await audit.PublishEvidenceToEpicAsync(epic, EvidenceRenderer.FromWorkerResult(
                        designerResult,
                        EvidenceTarget.Epic,
                        EvidenceStatus.Completed,
                        "product-designer",
                        "Planning completed",
                        "Expand the PRD milestones into defensible sprint briefs.",
                        "Read the PRD, resolve product ambiguity, write sprint briefs, and record any questions."));

                    await audit.PublishEvidenceToEpicAsync(epic, EvidenceRenderer.FromWorkerResult(
                        pmResult,
                        EvidenceTarget.Epic,
                        EvidenceStatus.Completed,
                        "pm",
                        "Sprint plan created",
                        "Convert sprint briefs into task-level plans that future workers can execute.",
                        "Read generated sprint briefs, split work into small tasks, write sprint JSON artifacts, and preserve traceability to the PRD."));
                }
            }
            catch (Exception ex)
            {
                var sprintName = PrdReader.ExtractLogicalSprintName(file);
                AnsiConsole.MarkupLine($"[yellow]  Warning: Failed to create issues for {Markup.Escape(sprintName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }

        Console.WriteLine();
        AnsiConsole.MarkupLine("[green]  ✓[/]  All sprint plans written to [bold]docs/sprints/[/]");
        Console.WriteLine();
        AnsiConsole.MarkupLine("[dim]  Review the plans, then run the build loop:[/]");
        AnsiConsole.MarkupLine("[bold]  piloop build --all[/]  [dim](build extraction pending)[/]");
        Console.WriteLine();
    }

    private async Task<PiWorkerResult> RunPlanningWorkerAsync(string runId, string taskId, string workerName, string taskInput)
    {
        var worker = _workerRegistry.Get(workerName);
        var prompt = await _contractBuilder.BuildAsync(worker.PromptPath, taskInput);
        var workerRuntime = await ResolveWorkerRuntimeAsync(workerName, worker.PromptPath);
        var rpcRunner = new PiRpcRunner(workerRuntime);

        ActivityLog.AgentStart(taskId, workerName, 1);
        var runResult = await rpcRunner.RunPromptAsync(StateManager.RepoRoot, prompt, worker.Timeout);
        var logPath = _tempLogs.GetWorkerLogPath(runId, taskId, workerName);
        await _tempLogs.WriteWorkerLogAsync(logPath, runResult.RawLines, runResult.DiagnosticSummary);

        if (runResult.Attempts > 1)
            ActivityLog.Info($"{workerName} succeeded after {runResult.Attempts} Pi attempt(s).");

        if (!runResult.Succeeded)
            ActivityLog.Error($"{workerName} failed with kind={runResult.FailureKind}; attempts={runResult.Attempts}; error={runResult.ErrorMessage ?? "none"}");

        var result = _resultValidator.ValidateRequired(runResult, workerName);
        ActivityLog.AgentDone(taskId, workerName, 1, result.Status is PiWorkerStatus.Success, result.Summary);

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

    private string BuildDesignerPrompt(bool hasAnswers)
    {
        var answersContext = hasAnswers
            ? $"""

                ## Previous Questions Answered

                The user has answered your previous questions. Read `{AnswersPath}` for their answers.
                Use these answers to resolve the ambiguities you flagged previously.
                Also read any previous sprint briefs in `docs/sprints/` to avoid duplicating work —
                build on what was already produced and refine it.
                """
            : "";

        return $"""
            # Plan All Milestones

            ## Your Mission

            Read the master PRD at `{_prdPath}`. Expand **every milestone** into a detailed sprint brief.

            Create the `docs/sprints/` directory if it doesn't exist.

            For each milestone, write a sprint brief to `docs/sprints/<YYYYMMDD>-<sprint-name>{BriefSuffix}`.
            Prefix with an 8-digit date stamp (`YYYYMMDD`) so sprint identities stay sortable and unique across parallel planning runs.
            Use a short kebab-case name derived from the milestone.
            Examples:
            - "Milestone 1 — Foundation" planned on 2026-04-11 → `20260411-foundation`
            - "Milestone 2 — The Weekly/Daily Loop" planned on 2026-04-11 → `20260411-weekly-daily`
            - "Milestone 3 — The Pomodoro Engine" planned on 2026-04-11 → `20260411-pomodoro-engine`
            {answersContext}

            ## What each sprint brief should cover

            - **User stories** for each feature in the milestone
            - **Screen descriptions** — what the user sees, what actions are available, mobile vs desktop
            - **Interaction details** — forms, validation, navigation, loading/empty/error states
            - **Edge cases and decisions** — resolve ambiguity, don't leave it open
            - **Out of scope** — what's explicitly not in this milestone
            - **Dependencies** — what must exist from prior milestones

            ## Questions

            If you encounter ambiguity that you cannot resolve from the PRD alone,
            write your questions to `{QuestionsPath}`.

            Format each question clearly with context about why you're asking:

            ```markdown
            ## Question 1: <topic>
            <context about what you're trying to decide>
            <the specific question>
            ```

            If you have NO questions, write `NO QUESTIONS` to `{QuestionsPath}`.

            ## Rules

            - Plan ALL milestones, not just the first one
            - Be specific — make UX decisions, don't leave options open
            - Each brief should stand alone but note dependencies on prior milestones
            - Read the full PRD before starting — context matters
            """;
    }

    private string BuildPmPrompt(bool hasAnswers)
    {
        var answersContext = hasAnswers
            ? $"""

                ## Previous Questions Answered

                Read `{AnswersPath}` for answers to previously flagged questions.
                """
            : "";

        var jsonExample = """
            {
              "sprint": "<sprint-name>",
              "description": "<one paragraph describing what this sprint delivers>",
              "createdAt": "<ISO timestamp>",
              "order": 1,
              "phases": {
                "domainModeling": true,
                "apiContract": true
              },
              "tasks": [
                {
                  "id": "TASK-001",
                  "title": "<short descriptive title>",
                  "type": "backend | frontend | both",
                  "description": "<detailed description>",
                  "acceptanceCriteria": [
                    "<specific, testable criterion>"
                  ]
                }
              ]
            }
            """;

        return $"""
            # Create Sprint Plans for All Milestones

            ## Your Mission

            Read the following inputs:
            1. The master PRD at `{_prdPath}`
            2. ALL sprint briefs in `docs/sprints/` — find the most recent brief for each sprint
               (files ending in `{BriefSuffix}` are from this run)

            For each sprint brief, produce a structured sprint plan JSON file.
            Use the same `<YYYYMMDD>-<sprint-name>` prefix from the brief filename.
            Output to `docs/sprints/<YYYYMMDD>-<sprint-name>{PrdSuffix}`.
            Example: brief `20260411-foundation{BriefSuffix}` → plan `20260411-foundation{PrdSuffix}`.

            Create the `docs/sprints/` directory if it doesn't exist.
            {answersContext}

            ## JSON Format

            Each sprint JSON must follow this exact format:

            ```json
            {jsonExample}
            ```

            ## Task Rules

            - **id**: Sequential within each sprint (TASK-001, TASK-002, etc.)
            - **type**: `backend` for .NET work, `frontend` for TanStack/React, `both` for full-stack tasks
            - **description**: Detailed enough that a builder agent can implement without guessing. Include which vertical slice, key patterns, and technical context.
            - **acceptanceCriteria**: Specific, testable conditions. These become the test-writer's input.
            - Each task should be completable by a single agent in one focused session
            - Respect vertical slice boundaries — no task crosses slice data boundaries
            - Set `domainModeling` and `apiContract` phases to true unless the sprint has no domain/API changes
            - **order**: Set to the milestone number (1 for Milestone 1, 2 for Milestone 2, etc.). This determines the build execution sequence inside a plan set. The dated sprint name is the unique sprint identity; `order` is the execution order.

            ## Questions

            If the sprint briefs have unresolved ambiguity that prevents you from creating clear tasks,
            **append** your questions to `{QuestionsPath}` (don't overwrite the Product Designer's questions).

            If you have no additional questions, do not modify the questions file.

            ## Rules

            - Create sprint JSONs for ALL sprint briefs, not just one
            - Do not invent requirements beyond what's in the PRD and sprint briefs
            - Keep tasks small — prefer many small tasks over few large ones
            - The build loop handles parallelism, so splitting is free
            """;
    }
}
