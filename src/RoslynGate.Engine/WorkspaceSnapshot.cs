using Microsoft.CodeAnalysis;

namespace RoslynGate.Engine;

public sealed record WorkspaceSnapshot(string SolutionPath, string RootPath, Solution Solution);
