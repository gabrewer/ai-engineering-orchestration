namespace PiLoop.Models;

public sealed record TargetProjectOptions(DirectoryInfo Root)
{
    public static TargetProjectOptions From(string? targetRoot)
    {
        var root = string.IsNullOrWhiteSpace(targetRoot)
            ? new DirectoryInfo(Directory.GetCurrentDirectory())
            : new DirectoryInfo(Path.GetFullPath(targetRoot));

        if (!root.Exists)
        {
            throw new DirectoryNotFoundException($"Target project root does not exist: {root.FullName}");
        }

        return new TargetProjectOptions(root);
    }

    public string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(Root.FullName, path));
}
