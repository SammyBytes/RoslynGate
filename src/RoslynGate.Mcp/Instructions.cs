namespace RoslynGate.Mcp;

public static class Instructions
{
    public const string Text =
        """
        RoslynGate answers .NET symbol questions with Roslyn, not text search.
        Use get_symbol_definition to resolve one symbol, find_symbol_references
        to locate semantic uses, get_type_hierarchy for inheritance, and
        search_symbols when you only know a name fragment.

        A target is either a qualified name or "file" plus a 1-based "line".
        Prefer detail=compact. It omits namespace, containing type, and column
        unless detail=verbose is requested.

        If a call returns isError, read error.hint and run error.try when present.
        """;
}
