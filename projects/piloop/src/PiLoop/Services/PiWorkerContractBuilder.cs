namespace PiLoop.Services;

public sealed class PiWorkerContractBuilder
{
    public async Task<string> BuildAsync(string promptPath, string taskInput)
    {
        if (!File.Exists(promptPath))
            throw new FileNotFoundException($"Pi worker prompt not found: {promptPath}");

        var rolePrompt = await File.ReadAllTextAsync(promptPath);
        return $"""
{rolePrompt.Trim()}

## Task Input

{taskInput.Trim()}
""";
    }
}
