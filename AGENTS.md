# AGENTS.md

RoslynGate is a stdio MCP server that answers .NET symbol questions with
Roslyn. Agents read this file before changing code.

## Invariants

1. `stdout` carries MCP messages only. Logs, banners, progress, and exceptions
   go to `stderr`. The SDK owns the transport.
2. Never guess. If a name matches more than one symbol, return candidates.
3. Determinism. Same workspace and query produce the same response: stable
   order, solution-relative paths, no timestamps, no random IDs.
4. Small files. Target 150 lines, hard limit 200. One concept per file.
   No `Utils`, `Helpers`, `Common`, `Managers`, or `Services` folders.
5. Layers flow one way. `Cli` composes, `Mcp` speaks MCP, `Engine` speaks
   Roslyn. `Engine` must not reference MCP. `Mcp` must not reference Roslyn.
6. Dependencies are deliberate. Pin them in `Directory.Packages.props` and
   justify new ones in the pull request.
7. Silence on success. The server does not narrate what it does.
8. Errors teach. Every actionable error has `code`, `message`, `hint`, and
   when possible `try` with the exact next call.
9. No configuration file. Flags override environment variables, which override
   defaults.
10. Token budget. Definitions are short, references are capped, and every
    response can be paginated.

## Layers

```
src/RoslynGate.Engine/   Discovery, loading, Roslyn queries, DTOs.
src/RoslynGate.Mcp/      MCP host, tools, JSON shapes, error catalog.
src/RoslynGate.Cli/      argv, logging, exit codes, composition.
tests/                   Engine, MCP contract, E2E.
fixtures/                Sample solution for deterministic tests.
```

## Workflow

1. Open `docs/ROADMAP.md` and pick the active task.
2. Write the failing test.
3. Implement the minimum to pass it.
4. Run `dotnet test` and `dotnet build -warnaserror`.
5. Update the roadmap and `docs/DECISIONS.md` if a decision changed.
6. Commit in Conventional Commits, English.

A task is done when the test is green, the build has no warnings, nothing
writes to `stdout` outside the transport, and the roadmap is updated.

## Commands

```bash
dotnet build RoslynGate.sln -warnaserror
dotnet test RoslynGate.sln
dotnet format RoslynGate.sln --verify-no-changes
dotnet run --project src/RoslynGate.Cli -- --solution fixtures/SampleSolution
dotnet run --project src/RoslynGate.Cli -- --check
```

## stdout smoke test

```bash
printf '%s\n' \
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"0"}}}' \
  | dotnet run --project src/RoslynGate.Cli -- --solution fixtures/SampleSolution
```

The output must be one valid JSON-RPC line. Any extra text is a failure.

## Error model

| Situation | Response |
| --- | --- |
| Malformed JSON | SDK parse error |
| Unknown method | SDK method not found |
| Invalid params | `isError: true`, `invalid_argument` |
| Solution load failure | `isError: true`, `solution_load_failed` |
| Symbol missing | Empty result, `isError: false` |
| Ambiguous symbol | Candidates, `isError: false` |
| Invalid solution at startup | `stderr` and exit 1 |

Exit codes: `0` normal end of input, `1` invalid configuration or solution,
`2` fatal startup failure.

## Out of scope

No HTTP, SSE, authentication, persistent index, code editing, test execution,
or text search fallback. No daemon and no on-disk cache.

## Skills

| Skill | Use |
| --- | --- |
| `csharp-expert` | C#, Roslyn, AOT, compiler APIs |
| `csharp-refactoring` | Safe refactors of the engine |
| `writing-plans` | Task planning |
| `release-changelog-style` | Versions, tags, changelog |
| `pdf-report-style` | Project PDFs |
| `find-skills` | Discover additional capabilities |
