using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RoslynGate.Engine;

public sealed class Workspace : IDisposable
{
    private static readonly object RegistrationGate = new();
    private static bool _msbuildRegistered;

    private readonly string? _solutionPath;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private WorkspaceSnapshot? _snapshot;
    private Task<WorkspaceSnapshot>? _loading;
    private MSBuildWorkspace? _msbuildWorkspace;
    private bool _disposed;

    public Workspace(string? solutionPath, Action<string>? log = null)
    {
        _solutionPath = solutionPath;
        _log = log ?? (_ => { });
    }

    public async Task<WorkspaceSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_snapshot is not null)
        {
            return _snapshot;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_snapshot is not null)
            {
                return _snapshot;
            }

            _loading ??= LoadCoreAsync(cancellationToken);

            try
            {
                _snapshot = await _loading.ConfigureAwait(false);
                return _snapshot;
            }
            catch
            {
                _loading = null;
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void StartPrewarm(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await GetAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
#pragma warning disable CA1031 // A prewarm failure must not crash the host.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                _log($"prewarm failed: {exception.Message}");
            }
        }, CancellationToken.None);
    }

    private async Task<WorkspaceSnapshot> LoadCoreAsync(CancellationToken cancellationToken)
    {
        var solutionPath = SolutionLocator.Locate(_solutionPath, Environment.CurrentDirectory);
        EnsureMSBuildRegistered();

        var msbuildWorkspace = MSBuildWorkspace.Create();
        msbuildWorkspace.SkipUnrecognizedProjects = true;
        msbuildWorkspace.LoadMetadataForReferencedProjects = false;

        var failures = new List<string>();
        msbuildWorkspace.RegisterWorkspaceFailedHandler(args =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            {
                failures.Add(args.Diagnostic.Message);
            }
        });

        try
        {
            var solution = await msbuildWorkspace
                .OpenSolutionAsync(solutionPath, progress: null, cancellationToken)
                .ConfigureAwait(false);

            if (failures.Count > 0)
            {
                throw new WorkspaceLoadException(
                    $"Failed to load {solutionPath}:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, failures.Take(5)));
            }

            if (!solution.Projects.Any())
            {
                throw new WorkspaceLoadException($"No projects loaded from {solutionPath}.");
            }

            _msbuildWorkspace = msbuildWorkspace;
            return new WorkspaceSnapshot(
                solutionPath,
                Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory,
                solution);
        }
        catch (OperationCanceledException)
        {
            msbuildWorkspace.Dispose();
            throw;
        }
        catch (WorkspaceLoadException)
        {
            msbuildWorkspace.Dispose();
            throw;
        }
#pragma warning disable CA1031 // Any MSBuild failure becomes an actionable load error.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            msbuildWorkspace.Dispose();
            throw new WorkspaceLoadException($"Failed to load {solutionPath}: {exception.Message}", exception);
        }
    }

    private static void EnsureMSBuildRegistered()
    {
        lock (RegistrationGate)
        {
            if (_msbuildRegistered)
            {
                return;
            }

            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            _msbuildRegistered = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _msbuildWorkspace?.Dispose();
        _gate.Dispose();
    }
}
