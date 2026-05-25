using System.CommandLine;
using PiLoop.Models;
using PiLoop.Services;
using Spectre.Console;

var targetRootOption = new Option<string?>("--target-root")
{
    Description = "Target project root. Defaults to the current working directory."
};

var piCommandOption = new Option<string?>("--pi-command")
{
    Description = "Pi executable or full path. Falls back to PI_CLI_PATH, then pi.cmd on Windows or pi elsewhere."
};
var piProviderOption = new Option<string?>("--pi-provider")
{
    Description = "Pi provider to use for worker processes, e.g. anthropic or openai."
};
var piModelOption = new Option<string?>("--pi-model")
{
    Description = "Override the model for all Pi worker processes. If omitted, each prompt may choose its own frontmatter model."
};
var piThinkingOption = new Option<string?>("--pi-thinking")
{
    Description = "Pi thinking level to use for worker processes: off, minimal, low, medium, high, xhigh."
};

var inspectCommand = new Command("inspect", "Inspect the target project and show PiLoop adoption status.")
{
    targetRootOption,
};
inspectCommand.SetAction(result =>
{
    try
    {
        var target = TargetProjectOptions.From(result.GetValue(targetRootOption));
        var inspector = new TargetProjectInspector(target.Root);
        inspector.WriteSummary(inspector.Inspect());
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        Environment.ExitCode = 1;
    }
});

var overwriteOption = new Option<bool>("--overwrite")
{
    Description = "Overwrite existing PiLoop prompt templates."
};

var initCommand = new Command("init", "Create PiLoop directories and default planning prompts in the target project.")
{
    targetRootOption,
    overwriteOption,
};
initCommand.SetAction(async (result, ct) =>
{
    try
    {
        var target = TargetProjectOptions.From(result.GetValue(targetRootOption));
        await PiLoopTemplateInstaller.InstallAsync(target.Root, result.GetValue(overwriteOption));

        var inspector = new TargetProjectInspector(target.Root);
        inspector.WriteSummary(inspector.Inspect());
        AnsiConsole.MarkupLine("[green]  ✓[/]  PiLoop target directories and planning prompts are present.");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        Environment.ExitCode = 1;
    }
});

var prdOption = new Option<string>("--prd")
{
    Description = "Target-repo-relative path to the markdown PRD.",
    Required = true,
};
var skipGitHubOption = new Option<bool>("--skip-github")
{
    Description = "Write local sprint artifacts but do not create or update GitHub issues."
};
var allowNewIssuesOption = new Option<bool>("--allow-new-issues")
{
    Description = "When reusing an existing sprint epic, create GitHub issues for newly generated task IDs."
};

var planCommand = new Command("plan", "Run the Pi-backed planning loop for a target project.")
{
    targetRootOption,
    prdOption,
    skipGitHubOption,
    allowNewIssuesOption,
    piCommandOption,
    piProviderOption,
    piModelOption,
    piThinkingOption,
};
planCommand.SetAction(async (result, ct) =>
{
    try
    {
        var target = TargetProjectOptions.From(result.GetValue(targetRootOption));
        var prd = result.GetValue(prdOption)!;
        var prdPath = target.ResolvePath(prd);
        var piRuntime = PiRuntimeOptions.From(
            result.GetValue(piCommandOption),
            result.GetValue(piProviderOption),
            result.GetValue(piModelOption),
            result.GetValue(piThinkingOption));

        if (!File.Exists(prdPath))
        {
            AnsiConsole.MarkupLine($"[red]PRD file does not exist: {Markup.Escape(prdPath)}[/]");
            Environment.ExitCode = 1;
            return;
        }

        await PiLoopTemplateInstaller.InstallAsync(target.Root);
        var loop = new PlanningLoop(
            target.Root,
            prd,
            piRuntime,
            publishGitHub: !result.GetValue(skipGitHubOption),
            allowNewGitHubIssues: result.GetValue(allowNewIssuesOption));
        await loop.ExecuteAsync();
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        Environment.ExitCode = 1;
    }
});

var buildSprintOption = new Option<string?>("--prd")
{
    Description = "Sprint name or target-repo-relative sprint JSON path to build."
};
var buildAllOption = new Option<bool>("--all")
{
    Description = "Build all discovered sprint plans in order."
};
var buildBranchOption = new Option<string?>("--branch")
{
    Description = "Branch to build on. Defaults to feature/<sprint>."
};
var noCommitOption = new Option<bool>("--no-commit")
{
    Description = "Do not commit after each task."
};
var buildResumeOption = new Option<bool>("--resume")
{
    Description = "Resume existing .piloop state for the sprint and skip tasks already marked done."
};
var noWorktreeOption = new Option<bool>("--no-worktree")
{
    Description = "Run build mode directly in the target root instead of creating a sibling wt/ worktree."
};

var buildCommand = new Command("build", "Run the Pi-backed build loop for sprint task implementation.")
{
    targetRootOption,
    buildSprintOption,
    buildAllOption,
    buildBranchOption,
    noCommitOption,
    buildResumeOption,
    noWorktreeOption,
    skipGitHubOption,
    piCommandOption,
    piProviderOption,
    piModelOption,
    piThinkingOption,
};

buildCommand.SetAction(async (result, ct) =>
{
    try
    {
        var target = TargetProjectOptions.From(result.GetValue(targetRootOption));
        StateManager.SetRepoRoot(target.Root.FullName);
        await PiLoopTemplateInstaller.InstallAsync(target.Root);
        var all = result.GetValue(buildAllOption);
        var sprint = result.GetValue(buildSprintOption);
        if (!all && string.IsNullOrWhiteSpace(sprint))
        {
            AnsiConsole.MarkupLine("[red]Specify --prd <sprint-name-or-json> or --all.[/]");
            Environment.ExitCode = 1;
            return;
        }

        var piRuntime = PiRuntimeOptions.From(
            result.GetValue(piCommandOption),
            result.GetValue(piProviderOption),
            result.GetValue(piModelOption),
            result.GetValue(piThinkingOption));
        var loop = new BuildLoop(target.Root, piRuntime, publishGitHub: !result.GetValue(skipGitHubOption));

        if (all)
        {
            foreach (var file in PrdReader.DiscoverAllSprintPlans())
                await loop.ExecuteAsync(
                    file,
                    result.GetValue(buildBranchOption),
                    commit: !result.GetValue(noCommitOption),
                    resume: result.GetValue(buildResumeOption),
                    useWorktree: !result.GetValue(noWorktreeOption));
        }
        else
        {
            await loop.ExecuteAsync(
                sprint!,
                result.GetValue(buildBranchOption),
                commit: !result.GetValue(noCommitOption),
                resume: result.GetValue(buildResumeOption),
                useWorktree: !result.GetValue(noWorktreeOption));
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        Environment.ExitCode = 1;
    }
});

var rootCommand = new RootCommand("piloop — Pi-native project orchestration")
{
    inspectCommand,
    initCommand,
    planCommand,
    buildCommand,
};

return await rootCommand.Parse(args).InvokeAsync();
