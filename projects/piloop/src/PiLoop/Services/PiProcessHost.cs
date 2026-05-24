using System.Diagnostics;

using PiLoop.Models;

namespace PiLoop.Services;

public sealed class PiProcessHost
{
    public string PiCommand { get; }

    public PiProcessHost(string piCommand = "pi")
    {
        PiCommand = piCommand;
    }

    public Process StartRpc(string workingDirectory, PiRuntimeOptions runtimeOptions, IEnumerable<string>? additionalArgs = null)
    {
        var psi = new ProcessStartInfo(runtimeOptions.PiCommand)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        psi.ArgumentList.Add("--mode");
        psi.ArgumentList.Add("rpc");
        psi.ArgumentList.Add("--no-session");

        if (!string.IsNullOrWhiteSpace(runtimeOptions.Provider))
        {
            psi.ArgumentList.Add("--provider");
            psi.ArgumentList.Add(runtimeOptions.Provider);
        }

        if (!string.IsNullOrWhiteSpace(runtimeOptions.Model))
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(runtimeOptions.Model);
        }

        if (!string.IsNullOrWhiteSpace(runtimeOptions.Thinking))
        {
            psi.ArgumentList.Add("--thinking");
            psi.ArgumentList.Add(runtimeOptions.Thinking);
        }

        if (additionalArgs is not null)
        {
            foreach (var arg in additionalArgs)
                psi.ArgumentList.Add(arg);
        }

        return Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start Pi process using command '{runtimeOptions.PiCommand}'.");
    }
}
