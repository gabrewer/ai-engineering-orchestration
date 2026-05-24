namespace PiLoop.Services;

public sealed class TempLogService
{
    public string GetWorkerLogPath(string runId, string taskId, string workerName, int? attempt = null) =>
        Models.StateManager.GetLogPath(runId, taskId, workerName, attempt);

    public async Task WriteWorkerLogAsync(string path, IEnumerable<string> lines, string? summary = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var writer = new StreamWriter(path, false);

        if (!string.IsNullOrWhiteSpace(summary))
        {
            await writer.WriteLineAsync(summary);
            await writer.WriteLineAsync();
        }

        foreach (var line in lines)
            await writer.WriteLineAsync(line);
    }
}
