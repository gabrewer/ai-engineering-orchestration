using System.Diagnostics;

namespace PiLoop.Services;

internal enum ChangeRequirement
{
    None,
    Tests,
    Implementation,
    AnySource,
}

internal sealed record WorktreeChange(string Path, string Status)
{
    public bool IsRelevantSource =>
        IsSourcePath(Path) &&
        !IsGeneratedOrBuildArtifact(Path) &&
        !IsPiLoopInternal(Path);

    public bool IsTestSource =>
        IsRelevantSource &&
        (Path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
         Path.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
         Path.Contains("/test/", StringComparison.OrdinalIgnoreCase) ||
         Path.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
         Path.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
         Path.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) ||
         Path.EndsWith(".spec.tsx", StringComparison.OrdinalIgnoreCase) ||
         Path.EndsWith(".test.tsx", StringComparison.OrdinalIgnoreCase));

    public bool IsImplementationSource =>
        IsRelevantSource && !IsTestSource && !Path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase);

    private static bool IsSourcePath(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        return ext is ".cs" or ".csproj" or ".sln" or ".props" or ".targets"
            or ".ts" or ".tsx" or ".js" or ".jsx" or ".json" or ".css"
            or ".html" or ".razor" or ".md";
    }

    private static bool IsGeneratedOrBuildArtifact(string path) =>
        path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/dist/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/coverage/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("FileListAbsolute.txt", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("project.assets.json", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("project.nuget.cache", StringComparison.OrdinalIgnoreCase);

    private static bool IsPiLoopInternal(string path) =>
        path.StartsWith(".piloop/", StringComparison.OrdinalIgnoreCase);
}

internal sealed class WorktreeSnapshot
{
    private readonly Dictionary<string, string> _changes;

    public WorktreeSnapshot(IReadOnlyList<WorktreeChange> changes)
    {
        _changes = changes.ToDictionary(c => c.Path, c => c.Status, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WorktreeChange> Diff(WorktreeSnapshot after) =>
        after._changes
            .Where(kvp => !_changes.TryGetValue(kvp.Key, out var beforeStatus) || beforeStatus != kvp.Value)
            .Select(kvp => new WorktreeChange(kvp.Key, kvp.Value))
            .OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

internal static class WorktreeChangeTracker
{
    public static async Task<WorktreeSnapshot> CaptureAsync(string workDir)
    {
        var output = await RunGitStatusAsync(workDir);
        return new WorktreeSnapshot(ParseStatus(output));
    }

    public static string? Validate(ChangeRequirement requirement, IReadOnlyList<WorktreeChange> changes)
    {
        if (requirement == ChangeRequirement.None)
            return null;

        var relevant = changes.Where(c => c.IsRelevantSource).ToArray();
        var ok = requirement switch
        {
            ChangeRequirement.Tests => relevant.Any(c => c.IsTestSource),
            ChangeRequirement.Implementation => relevant.Any(c => c.IsImplementationSource),
            ChangeRequirement.AnySource => relevant.Length > 0,
            _ => true,
        };

        if (ok) return null;

        var changed = relevant.Length == 0
            ? "No relevant source/test files changed."
            : "Relevant changes were: " + string.Join(", ", relevant.Take(12).Select(c => c.Path));

        return requirement switch
        {
            ChangeRequirement.Tests => $"Expected test source changes, but none were produced. {changed}",
            ChangeRequirement.Implementation => $"Expected implementation source changes, but none were produced. {changed}",
            ChangeRequirement.AnySource => $"Expected source changes, but none were produced. {changed}",
            _ => changed,
        };
    }

    private static async Task<string> RunGitStatusAsync(string workDir)
    {
        var psi = new ProcessStartInfo("git", "status --porcelain=v1 -z --untracked-files=all")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git status.");
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"git status failed: {stderr.Trim()}");

        return stdout;
    }

    private static IReadOnlyList<WorktreeChange> ParseStatus(string output)
    {
        var entries = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var changes = new List<WorktreeChange>();

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 4) continue;

            var status = entry[..2];
            var path = Normalize(entry[3..]);

            if (status[0] is 'R' or 'C')
            {
                // Porcelain -z emits the old path as the next entry for renames/copies.
                if (i + 1 < entries.Length) i++;
            }

            changes.Add(new WorktreeChange(path, status));
        }

        return changes;
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim();
}
