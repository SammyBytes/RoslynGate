using System.Diagnostics;

namespace RoslynGate.Engine;

public sealed record DotNetSdk(string Version, string Path);

public static class DotNetSdkLocator
{
    public static DotNetSdk? Find(string workingDirectory)
    {
        var executable = FindExecutable();
        var globalJson = GlobalJson.Find(workingDirectory);

        if (executable is null)
        {
            if (globalJson is not null)
            {
                throw MissingSdk(globalJson, requested: null, installed: []);
            }

            return null;
        }

        var versions = ListVersions(executable, workingDirectory);
        if (versions.Count == 0)
        {
            if (globalJson is not null)
            {
                throw MissingSdk(globalJson, requested: null, installed: []);
            }

            return null;
        }

        var selected = Run(executable, workingDirectory, "--version")?.Trim();
        if (selected is not null)
        {
            var exact = versions.FirstOrDefault(
                version => string.Equals(version.Version, selected, StringComparison.Ordinal));
            if (exact is not null)
            {
                return exact;
            }
        }

        var requested = globalJson is null ? null : GlobalJson.ReadRequestedVersion(globalJson);
        if (globalJson is not null && requested is not null)
        {
            var requestedSdk = versions.FirstOrDefault(
                version => string.Equals(version.Version, requested, StringComparison.Ordinal));
            if (requestedSdk is not null)
            {
                return requestedSdk;
            }

            throw MissingSdk(globalJson, requested, versions);
        }

        return versions
            .OrderByDescending(version => ParseVersion(version.Version))
            .First();
    }

    private static WorkspaceLoadException MissingSdk(
        string globalJson,
        string? requested,
        List<DotNetSdk> installed)
    {
        var requirement = requested is null ? "the requested SDK" : $".NET SDK {requested}";
        var available = installed.Count == 0
            ? "none"
            : string.Join(", ", installed.Select(version => version.Version));
        return new WorkspaceLoadException(
            $"global.json requires {requirement}, but it is not installed.",
            $"Install {requirement} or update {globalJson}. Installed SDKs: {available}. Run 'dotnet --list-sdks' to verify.");
    }

    private static List<DotNetSdk> ListVersions(string executable, string workingDirectory)
    {
        var output = Run(executable, workingDirectory, "--list-sdks");
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var sdks = new List<DotNetSdk>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var open = line.IndexOf('[', StringComparison.Ordinal);
            var close = line.LastIndexOf(']');
            if (open <= 0 || close <= open)
            {
                continue;
            }

            var version = line[..open].Trim();
            var root = line[(open + 1)..close].Trim();
            if (version.Length == 0 || root.Length == 0)
            {
                continue;
            }

            sdks.Add(new DotNetSdk(version, Path.Combine(root, version)));
        }

        return sdks;
    }

    private static string? FindExecutable()
    {
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
        {
            var candidate = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
    }

    private static string? Run(string executable, string workingDirectory, string argument)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { argument }
            });

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception exception) when (
            exception is IOException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static Version ParseVersion(string value)
    {
        var stable = value.Split('-', 2)[0];
        return Version.TryParse(stable, out var version)
            ? version
            : new Version(0, 0);
    }
}
