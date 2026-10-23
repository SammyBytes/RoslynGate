using System.Diagnostics;

namespace RoslynGate.E2E.Tests;

internal static class CliRunner
{
    public static async Task<CliResult> RunAsync(string workingDirectory, string input, params string[] args)
    {
        var process = Start(workingDirectory, args);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            if (input.Length > 0)
            {
                await process.StandardInput.WriteAsync(input);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync();

            return new CliResult(process.ExitCode, await stdout, await stderr);
        }
        finally
        {
            process.Dispose();
        }
    }

    public static Process Start(string workingDirectory, params string[] args)
    {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrWhiteSpace(dotnet))
        {
            dotnet = "dotnet";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = dotnet,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "RoslynGate.Cli.dll"));
        foreach (var argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Failed to start the RoslynGate CLI.");
        }

        return process;
    }
}

internal sealed record CliResult(int ExitCode, string Stdout, string Stderr);
