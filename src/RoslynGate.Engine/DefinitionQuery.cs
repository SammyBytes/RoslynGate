namespace RoslynGate.Engine;

public static class DefinitionQuery
{
    public static async Task<DefinitionResult> GetAsync(
        WorkspaceSnapshot snapshot,
        SymbolTarget target,
        CancellationToken cancellationToken)
    {
        var resolution = await SymbolResolver
            .ResolveAsync(snapshot, target, cancellationToken)
            .ConfigureAwait(false);

        return Build(resolution);
    }

    internal static DefinitionResult Build(SymbolResolution resolution)
    {
        if (resolution.Matches.Count == 0)
        {
            return new DefinitionResult(null, Ambiguous: false, Array.Empty<SymbolDefinition>());
        }

        if (resolution.Matches.Count == 1)
        {
            return new DefinitionResult(
                resolution.Matches[0].Definition,
                Ambiguous: false,
                Array.Empty<SymbolDefinition>());
        }

        return new DefinitionResult(
            null,
            Ambiguous: true,
            resolution.Matches
                .Take(DefinitionLimits.MaxCandidates)
                .Select(match => match.Definition)
                .ToArray());
    }
}
