namespace PiLoop.Services;

public sealed record PiWorkerDefinition(string Name, string PromptPath, TimeSpan Timeout);

public sealed class PiWorkerRegistry
{
    private readonly Dictionary<string, PiWorkerDefinition> _workers;

    public PiWorkerRegistry(string? repoRoot = null)
    {
        var root = repoRoot ?? Directory.GetCurrentDirectory();
        var promptsDir = Path.Combine(root, ".pi", "prompts");

        _workers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["product-designer"] = new("product-designer", Path.Combine(promptsDir, "product-designer.md"), TimeSpan.FromMinutes(20)),
            ["pm"] = new("pm", Path.Combine(promptsDir, "pm.md"), TimeSpan.FromMinutes(20)),
            ["domain-modeler"] = new("domain-modeler", Path.Combine(promptsDir, "domain-modeler.md"), TimeSpan.FromMinutes(20)),
            ["api-developer"] = new("api-developer", Path.Combine(promptsDir, "api-developer.md"), TimeSpan.FromMinutes(20)),
            ["test-writer"] = new("test-writer", Path.Combine(promptsDir, "test-writer.md"), TimeSpan.FromMinutes(20)),
            ["backend-builder"] = new("backend-builder", Path.Combine(promptsDir, "backend-builder.md"), TimeSpan.FromMinutes(30)),
            ["frontend-builder"] = new("frontend-builder", Path.Combine(promptsDir, "frontend-builder.md"), TimeSpan.FromMinutes(30)),
            ["destroyer"] = new("destroyer", Path.Combine(promptsDir, "destroyer.md"), TimeSpan.FromMinutes(15)),
            ["review-agent"] = new("review-agent", Path.Combine(promptsDir, "review-agent.md"), TimeSpan.FromMinutes(15)),
            ["git-committer"] = new("git-committer", Path.Combine(promptsDir, "git-committer.md"), TimeSpan.FromMinutes(10)),
        };
    }

    public PiWorkerDefinition Get(string workerName)
    {
        if (_workers.TryGetValue(workerName, out var worker))
            return worker;

        throw new InvalidOperationException($"No Pi worker definition found for '{workerName}'.");
    }
}
