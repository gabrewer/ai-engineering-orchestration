using System.Diagnostics;
using Spectre.Console;

namespace PiLoop.Services;

/// <summary>
/// Git operations including worktree management. All operations run against a specific
/// working directory (either the main repo or a worktree).
/// </summary>
public sealed class GitService
{
    private readonly string _workDir;

    public GitService(string workDir)
    {
        _workDir = workDir;
    }

    /// <summary>The working directory this service operates in.</summary>
    public string WorkDir => _workDir;

    /// <summary>
    /// Ensures the working directory is a git repo with at least one commit.
    /// </summary>
    public async Task EnsureRepoAsync()
    {
        if (!Directory.Exists(Path.Combine(_workDir, ".git")) &&
            !File.Exists(Path.Combine(_workDir, ".git"))) // worktrees have a .git file, not directory
        {
            AnsiConsole.MarkupLine("[dim]  No git repo found. Initializing...[/]");
            await RunAsync("init");
        }

        if (!await HasCommitsAsync())
        {
            AnsiConsole.MarkupLine("[dim]  No commits found. Creating initial commit...[/]");
            await RunAsync("add", ".");
            await RunAsync("commit", "-m", "Initial commit");
        }
    }

    public async Task<string> GetCurrentBranchAsync()
    {
        await EnsureRepoAsync();

        var text = await RunCaptureAsync("branch", "--show-current");
        var branch = text.Trim();
        return string.IsNullOrEmpty(branch) ? "main" : branch;
    }

    public async Task<bool> BranchExistsAsync(string branchName)
    {
        var exitCode = await RunSilentAsync("rev-parse", "--verify", $"refs/heads/{branchName}");
        return exitCode == 0;
    }

    public async Task<bool> HasCommitsAsync()
    {
        var exitCode = await RunSilentAsync("rev-parse", "HEAD");
        return exitCode == 0;
    }

    /// <summary>
    /// Creates a stacked branch off a parent and checks it out.
    /// Worktree-safe: uses the parent's commit ref instead of checking out the parent branch
    /// (which may be locked by another worktree).
    /// </summary>
    public async Task CreateStackedBranchAsync(string branchName, string parentBranch)
    {
        if (await BranchExistsAsync(branchName))
        {
            // Branch exists — check it out only if we're not already on it
            var current = await GetCurrentBranchAsync();
            if (current != branchName)
                await RunAsync("checkout", branchName);
            try
            {
                // Merge from the parent ref (not checkout) — safe even if parent is locked
                await RunAsync("merge", parentBranch, "--no-edit");
            }
            catch
            {
                AnsiConsole.MarkupLine($"[yellow]  Warning: merge conflict from {Markup.Escape(parentBranch)} into {Markup.Escape(branchName)}. Continuing.[/]");
            }
            AnsiConsole.MarkupLine($"[dim]  Checked out stacked branch: {Markup.Escape(branchName)} (based on {Markup.Escape(parentBranch)})[/]");
        }
        else
        {
            // Create new branch from the parent's commit — doesn't require checking out parent
            await RunAsync("checkout", "-b", branchName, parentBranch);
            AnsiConsole.MarkupLine($"[dim]  Created stacked branch: {Markup.Escape(branchName)} (based on {Markup.Escape(parentBranch)})[/]");
        }
    }

    /// <summary>
    /// Ensures we're on the target branch, creating it if necessary.
    /// </summary>
    public async Task<string> EnsureBranchAsync(string? branchFlag, string? sprintName = null)
    {
        var current = await GetCurrentBranchAsync();

        if (current is not "main" and not "master" && branchFlag is null)
            return current;

        var targetBranch = branchFlag ?? (sprintName is not null ? $"feature/{sprintName}" : null);
        if (targetBranch is null)
        {
            AnsiConsole.MarkupLine("[red]On main/master with no --branch and no sprint name. Specify --branch.[/]");
            Environment.Exit(1);
        }

        if (current != targetBranch)
        {
            if (await BranchExistsAsync(targetBranch!))
            {
                await RunAsync("checkout", targetBranch!);
                AnsiConsole.MarkupLine($"[dim]  Checked out: {Markup.Escape(targetBranch!)}[/]");
            }
            else
            {
                await RunAsync("checkout", "-b", targetBranch!);
                AnsiConsole.MarkupLine($"[dim]  Created branch: {Markup.Escape(targetBranch!)}[/]");
            }
        }

        return targetBranch!;
    }

    // ─── Worktree management ─────────────────────────────────────────

    /// <summary>
    /// Creates a git worktree for the build loop. Returns a new GitService pointing at the worktree.
    /// The worktree is created under the sibling wt/ directory.
    /// </summary>
    public async Task<GitService> CreateWorktreeAsync(string name, string baseBranch)
    {
        var repoRoot = _workDir;
        var parent = Directory.GetParent(repoRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not determine parent directory for repo root: {repoRoot}");
        var parentDir = Path.GetFileName(parent).Equals("wt", StringComparison.OrdinalIgnoreCase)
            ? parent
            : Path.Combine(parent, "wt");
        Directory.CreateDirectory(parentDir);

        var worktreePath = Path.GetFullPath(Path.Combine(parentDir, name));

        if (Directory.Exists(worktreePath))
        {
            // Worktree already exists — reuse it
            AnsiConsole.MarkupLine($"[dim]  Reusing existing worktree: {Markup.Escape(worktreePath)}[/]");
            return new GitService(worktreePath);
        }

        // Create a new branch for the worktree (git worktree add requires a branch)
        var worktreeBranch = $"wt/{name}";
        if (await BranchExistsAsync(worktreeBranch))
        {
            // Branch exists, create worktree checking it out
            await RunAsync("worktree", "add", worktreePath, worktreeBranch);
        }
        else
        {
            // Create worktree with new branch based on the base branch
            await RunAsync("worktree", "add", "-b", worktreeBranch, worktreePath, baseBranch);
        }

        AnsiConsole.MarkupLine($"[dim]  Created worktree: {Markup.Escape(worktreePath)}[/]");
        return new GitService(worktreePath);
    }

    /// <summary>
    /// Removes a worktree. Safe to call if worktree doesn't exist.
    /// </summary>
    public async Task RemoveWorktreeAsync(string worktreePath)
    {
        try
        {
            await RunAsync("worktree", "remove", worktreePath, "--force");
            AnsiConsole.MarkupLine($"[dim]  Removed worktree: {Markup.Escape(worktreePath)}[/]");
        }
        catch
        {
            // Worktree might not exist or be locked — not fatal
        }
    }

    // ─── Process helpers ─────────────────────────────────────────────

    public async Task RunAsync(params string[] gitArgs)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _workDir,
        };
        foreach (var arg in gitArgs) psi.ArgumentList.Add(arg);
        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            var stderr = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"git {string.Join(' ', gitArgs)} failed: {stderr.Trim()}");
        }
    }

    private async Task<string> RunCaptureAsync(params string[] gitArgs)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _workDir,
        };
        foreach (var arg in gitArgs) psi.ArgumentList.Add(arg);
        using var proc = Process.Start(psi)!;
        var text = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return text;
    }

    private async Task<int> RunSilentAsync(params string[] gitArgs)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _workDir,
        };
        foreach (var arg in gitArgs) psi.ArgumentList.Add(arg);
        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }
}
