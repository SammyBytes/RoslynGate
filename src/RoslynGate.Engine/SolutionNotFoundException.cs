namespace RoslynGate.Engine;

public sealed class SolutionNotFoundException(string message) : Exception(message);
