using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace RoslynGate.Engine;

public sealed partial class Workspace
{
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;

    private void StartWatcher(string root)
    {
        if (!_watch || _watcher is not null)
        {
            return;
        }

        try
        {
            _debounce = new Timer(_ => _ = ApplyPendingAsync(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
            };
            _watcher.Changed += (_, args) => Schedule(args.FullPath);
            _watcher.Created += (_, args) => Schedule(args.FullPath);
            _watcher.Deleted += (_, args) => Schedule(args.FullPath);
            _watcher.Renamed += (_, args) =>
            {
                Schedule(args.OldFullPath);
                Schedule(args.FullPath);
            };
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "File watcher could not start; freshness checks are disabled.");
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void Schedule(string path)
    {
        if (!_watch || IsIgnored(path))
        {
            return;
        }

        var extension = Path.GetExtension(path);
        var structural = extension is ".csproj" or ".props" or ".targets" or ".sln" or ".razor" or ".slnx"
            || string.Equals(Path.GetFileName(path), "global.json", StringComparison.OrdinalIgnoreCase);
        if (!structural && !string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_stateGate)
        {
            _pending.Add(path);
            _changed.Add(Relative(path));
            _stale = true;
            _structuralStale |= structural;
        }

        _debounce?.Change(400, Timeout.Infinite);
    }

    private async Task ApplyPendingAsync()
    {
        await _updateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            string[] paths;
            lock (_stateGate)
            {
                paths = _pending.ToArray();
                _pending.Clear();
            }

            var snapshot = _snapshot;
            if (snapshot is null)
            {
                return;
            }

            var solution = snapshot.Solution;
            foreach (var path in paths.Where(path => string.Equals(
                Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    solution = ApplyCsChange(solution, path);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not apply workspace change for {Path}.", path);
                    lock (_stateGate)
                    {
                        _structuralStale = true;
                    }
                }
            }

            _snapshot = snapshot with { Solution = solution };
            lock (_stateGate)
            {
                if (!_structuralStale)
                {
                    _stale = false;
                    _changed.Clear();
                }
            }
        }
        finally
        {
            _updateGate.Release();
        }
    }

    private static Solution ApplyCsChange(Solution solution, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var documentIds = solution.GetDocumentIdsWithFilePath(fullPath).ToArray();

        if (!File.Exists(fullPath))
        {
            foreach (var documentId in documentIds)
            {
                solution = solution.RemoveDocument(documentId);
            }

            return solution;
        }

        var text = SourceText.From(File.ReadAllText(fullPath));
        if (documentIds.Length > 0)
        {
            foreach (var documentId in documentIds)
            {
                solution = solution.WithDocumentText(documentId, text);
            }

            return solution;
        }

        var project = FindProject(solution, fullPath);
        if (project is null)
        {
            throw new InvalidOperationException($"No project contains {fullPath}.");
        }

        var document = project.AddDocument(
            Path.GetFileName(fullPath),
            text,
            filePath: fullPath);
        return document.Project.Solution;
    }
    private static Project? FindProject(Solution solution, string file)
    {
        return solution.Projects
            .Where(project => project.FilePath is not null)
            .Select(project => new
            {
                Project = project,
                Directory = Path.GetDirectoryName(project.FilePath!) ?? string.Empty
            })
            .Where(item => file.StartsWith(
                item.Directory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Directory.Length)
            .Select(item => item.Project)
            .FirstOrDefault();
    }

    private string Relative(string path)
    {
        var root = _snapshot?.RootPath;
        return root is null
            ? path
            : Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static bool IsIgnored(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.vs/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
    }
}
