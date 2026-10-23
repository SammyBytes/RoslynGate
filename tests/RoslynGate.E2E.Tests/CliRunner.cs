using System.Diagnostics;

namespace RoslynGate.E2E.Tests;

internal static class CliRunner
{
    public static Task<CliResult> RunAsync(string workingDirectory, string input, params string[] args)
        => RunAsync(workingDirectory, input, null, args);

    public static async Task<CliResult> RunAsync(
        string workingDirectory,
        string input,
        IReadOnlyDictionary<string, string?>? environment,
        params string[] args)
    {
        var process = Start(workingDirectory, environment, args);
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
        => Start(workingDirectory, null, args);

    private static Process Start(
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
        params string[] args)
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

        foreach (var key in startInfo.Environment.Keys
            .Where(key => key.StartsWith("ROSLYGATE_", StringComparison.Ordinal))
            .ToArray())
        {
            startInfo.Environment.Remove(key);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "roslyngate.dll"));
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
