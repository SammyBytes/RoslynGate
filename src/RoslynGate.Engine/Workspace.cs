using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RoslynGate.Engine;

public sealed partial class Workspace : IDisposable
{
    private readonly string? _solutionPath;
    private readonly ILogger<Workspace> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly bool _watch;

    private WorkspaceSnapshot? _snapshot;
    private Task<WorkspaceSnapshot>? _loading;
    private MSBuildWorkspace? _msbuildWorkspace;
    private IReadOnlyList<string> _diagnostics = Array.Empty<string>();
    private bool _disposed;

    public IReadOnlyList<string> Diagnostics => _diagnostics;

    public static DotNetSdk? Sdk => MsBuildRegistration.Sdk;

    public bool WatchEnabled => _watch;

    public Workspace(string? solutionPath, ILogger<Workspace>? logger = null, bool watch = true)
    {
        _solutionPath = solutionPath;
        _logger = logger ?? NullLogger<Workspace>.Instance;
        _watch = watch;
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
                _logger.LogError(exception, "Prewarm failed.");
            }
        }, CancellationToken.None);
    }

    private async Task<WorkspaceSnapshot> LoadCoreAsync(CancellationToken cancellationToken)
    {
        var solutionPath = SolutionLocator.Locate(_solutionPath, Environment.CurrentDirectory);
        MsBuildRegistration.Ensure(Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory);

        var msbuildWorkspace = MSBuildWorkspace.Create();
        msbuildWorkspace.SkipUnrecognizedProjects = true;
        msbuildWorkspace.LoadMetadataForReferencedProjects = false;

        var failures = new List<string>();
        msbuildWorkspace.RegisterWorkspaceFailedHandler(args =>
        {
            failures.Add(args.Diagnostic.Message);
            _logger.LogWarning("MSBuild: {Message}", args.Diagnostic.Message);
        });

        try
        {
            var solution = await msbuildWorkspace
                .OpenSolutionAsync(solutionPath, progress: null, cancellationToken)
                .ConfigureAwait(false);

            // Legacy solutions report package-compatibility warnings as MSBuild
            // failures. Keep the loaded projects usable and surface the warnings.
            if (!solution.Projects.Any())
            {
                var details = failures.Count == 0
                    ? string.Empty
                    : Environment.NewLine + string.Join(
                        Environment.NewLine,
                        failures.Take(WorkspaceLimits.MaxLoadDiagnostics));
                throw new WorkspaceLoadException($"No projects loaded from {solutionPath}.{details}");
            }

            _diagnostics = failures.Take(WorkspaceLimits.MaxLoadDiagnostics).ToArray();
            _msbuildWorkspace = msbuildWorkspace;
            var snapshot = new WorkspaceSnapshot(
                solutionPath,
                Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory,
                solution);
            StartWatcher(snapshot.RootPath);
            return snapshot;
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeFreshness();
        _msbuildWorkspace?.Dispose();
        _gate.Dispose();
    }
}
