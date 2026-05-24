using System.Runtime.InteropServices;

namespace PiLoop.Models;

public sealed record PiRuntimeOptions(
    string PiCommand,
    string? Provider = null,
    string? Model = null,
    string? Thinking = null,
    int MaxAttempts = 2)
{
    public static PiRuntimeOptions From(string? piCommand = null, string? provider = null, string? model = null, string? thinking = null)
    {
        var resolvedCommand =
            FirstNonEmpty(piCommand)
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("PI_CLI_PATH"))
            ?? GetDefaultPiCommand();

        return new PiRuntimeOptions(resolvedCommand, FirstNonEmpty(provider), FirstNonEmpty(model), FirstNonEmpty(thinking), 2);
    }

    private static string GetDefaultPiCommand() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pi.cmd" : "pi";

    private static string? FirstNonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
