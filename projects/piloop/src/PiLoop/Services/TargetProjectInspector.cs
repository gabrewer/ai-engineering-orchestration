using Spectre.Console;

namespace PiLoop.Services;

public sealed class TargetProjectInspector
{
    private readonly DirectoryInfo _root;

    public TargetProjectInspector(DirectoryInfo root)
    {
        _root = root;
    }

    public TargetProjectSnapshot Inspect()
    {
        return new TargetProjectSnapshot(
            Root: _root.FullName,
            HasGit: IsInsideGitWorkTree(),
            HasTeamOrchestration: File.Exists(Path.Combine(_root.FullName, "TEAM-ORCHESTRATION.md")),
            HasPiPrompts: Directory.Exists(Path.Combine(_root.FullName, ".pi", "prompts")),
            HasAgentSkills: Directory.Exists(Path.Combine(_root.FullName, ".agents", "skills")),
            HasSprintDocs: Directory.Exists(Path.Combine(_root.FullName, "docs", "sprints")));
    }

    public void WriteSummary(TargetProjectSnapshot snapshot)
    {
        AnsiConsole.MarkupLine("[rgb(99,102,241)]⬡[/]  [bold]PiLoop target project[/]");
        AnsiConsole.MarkupLine($"[dim]  Root: {Markup.Escape(snapshot.Root)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Git repo: {FormatBool(snapshot.HasGit)}[/]");
        AnsiConsole.MarkupLine($"[dim]  TEAM-ORCHESTRATION.md: {FormatBool(snapshot.HasTeamOrchestration)}[/]");
        AnsiConsole.MarkupLine($"[dim]  .pi/prompts: {FormatBool(snapshot.HasPiPrompts)}[/]");
        AnsiConsole.MarkupLine($"[dim]  .agents/skills: {FormatBool(snapshot.HasAgentSkills)}[/]");
        AnsiConsole.MarkupLine($"[dim]  docs/sprints: {FormatBool(snapshot.HasSprintDocs)}[/]");
    }

    private bool IsInsideGitWorkTree()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --is-inside-work-tree",
                WorkingDirectory = _root.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(2_000);
            return process.ExitCode == 0 && process.StandardOutput.ReadToEnd().Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string FormatBool(bool value) => value ? "[green]yes[/]" : "[yellow]no[/]";
}

public sealed record TargetProjectSnapshot(
    string Root,
    bool HasGit,
    bool HasTeamOrchestration,
    bool HasPiPrompts,
    bool HasAgentSkills,
    bool HasSprintDocs);
