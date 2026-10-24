using Microsoft.CodeAnalysis.MSBuild;

namespace RoslynGate.Engine;

public sealed partial class Workspace
{
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly HashSet<string> _changed = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MSBuildWorkspace> _oldWorkspaces = [];
    private bool _structuralStale;
    private bool _stale;

    public WorkspaceState GetState()
    {
        lock (_stateGate)
        {
            return new WorkspaceState(
                _stale,
                _changed.OrderBy(path => path, StringComparer.Ordinal).ToArray());
        }
    }

    public bool IsFileIndexed(string file)
    {
        var snapshot = _snapshot;
        if (snapshot is null)
        {
            return false;
        }

        var path = Path.IsPathRooted(file)
            ? Path.GetFullPath(file)
            : Path.GetFullPath(Path.Combine(snapshot.RootPath, file));
        return snapshot.Solution.GetDocumentIdsWithFilePath(path).Any();
    }

    public async Task<WorkspaceSnapshot> ReloadAsync(CancellationToken cancellationToken)
    {
        await _updateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var previousSnapshot = _snapshot;
                var previousWorkspace = _msbuildWorkspace;

                _snapshot = null;
                _loading = null;
                _msbuildWorkspace = null;

                try
                {
                    var snapshot = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
                    _snapshot = snapshot;
                    ClearStale();
                    return snapshot;
                }
                catch
                {
                    _snapshot = previousSnapshot;
                    _msbuildWorkspace = previousWorkspace;
                    throw;
                }
                finally
                {
                    if (previousWorkspace is not null && !ReferenceEquals(previousWorkspace, _msbuildWorkspace))
                    {
                        _oldWorkspaces.Add(previousWorkspace);
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            _updateGate.Release();
        }
    }

    private void ClearStale()
    {
        lock (_stateGate)
        {
            _stale = false;
            _structuralStale = false;
            _changed.Clear();
        }
    }

    private void DisposeFreshness()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        _updateGate.Dispose();
        foreach (var workspace in _oldWorkspaces)
        {
            workspace.Dispose();
        }
    }
}
