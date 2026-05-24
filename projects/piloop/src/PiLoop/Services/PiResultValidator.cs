using PiLoop.Models;

namespace PiLoop.Services;

public sealed class PiResultValidator
{
    public PiWorkerResult ValidateRequired(PiWorkerRunResult runResult, string workerName)
    {
        if (!runResult.Succeeded)
            throw new InvalidOperationException(
                $"Pi worker '{workerName}' failed. kind={runResult.FailureKind}; attempts={runResult.Attempts}; error={runResult.ErrorMessage ?? "none"}");

        if (runResult.FinalResult is null)
            throw new InvalidOperationException($"Pi worker '{workerName}' did not emit a final structured JSON result.");

        return runResult.FinalResult;
    }
}
