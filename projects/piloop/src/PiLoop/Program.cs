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
    Description = "Pi model to use for worker processes."
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

var planCommand = new Command("plan", "Run the Pi-backed planning loop for a target project.")
{
    targetRootOption,
    prdOption,
    skipGitHubOption,
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
        var loop = new PlanningLoop(target.Root, prd, piRuntime, publishGitHub: !result.GetValue(skipGitHubOption));
        await loop.ExecuteAsync();
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
};

return await rootCommand.Parse(args).InvokeAsync();
