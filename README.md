# RoslynGate

[![ci](https://github.com/SammyBytes/RoslynGate/actions/workflows/ci.yml/badge.svg)](https://github.com/SammyBytes/RoslynGate/actions/workflows/ci.yml)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

RoslynGate is a stdio MCP server that answers .NET symbol questions with
Roslyn instead of text search. It returns compact, deterministic results so an
agent can find a definition, reference, or hierarchy without dumping code into
the context window.

## Tools

| Tool | Purpose |
| --- | --- |
| `get_symbol_definition` | Resolve one symbol by qualified name or file + line. |
| `find_symbol_references` | Find semantic references, paginated. |
| `get_type_hierarchy` | Base types, interfaces, derived types, implementations. |
| `search_symbols` | Find symbols by name fragment before resolving. |

Every response is JSON. `detail=compact` (default) keeps the wire small;
`detail=verbose` adds namespace, container, column, and longer documentation.

## Requirements

- .NET 10 SDK
- Linux, macOS, or Windows

## Build

```bash
dotnet build RoslynGate.sln -warnaserror
dotnet test RoslynGate.sln
```

## Run

```bash
dotnet run --project src/RoslynGate.Cli -- --solution fixtures/SampleSolution
```

The process speaks MCP over stdin/stdout. Diagnostics go to stderr. Close stdin
to stop it.

## Connect

```json
{
  "mcpServers": {
    "roslyngate": {
      "command": "/path/to/roslyngate",
      "args": ["--solution", "/path/to/repository"]
    }
  }
}
```

## Test with OpenCode

The repository ships `opencode.jsonc`, a local stdio server entry. It is
disabled until the MCP host exists.

1. Run `dotnet build`.
2. Remove `"disabled": true` from `opencode.jsonc`.
3. Start OpenCode in this repository and run `/mcps`.
4. Confirm `roslyngate connected`.
5. Ask the agent to use `roslyngate` to find a definition.

The command runs the built DLL, not `dotnet run`, so build output cannot
corrupt the MCP stdout stream. For a published binary, replace `command` with
the binary path and add `--solution /path/to/repository`.

## Check

`--check` validates the solution and configuration without starting the
protocol. It exits `0` when the workspace is usable and `1` otherwise.

```bash
roslyngate --solution . --check
```

## Configuration

Flags override environment variables, which override defaults.

| Flag | Environment | Default |
| --- | --- | --- |
| `-s, --solution` | `ROSLYGATE_SOLUTION` | auto-detect |
| `-l, --log-level` | `ROSLYGATE_LOG_LEVEL` | `error` |
| `--detail` | `ROSLYGATE_DETAIL` | `compact` |
| `--ref-limit` | `ROSLYGATE_REF_LIMIT` | `50` (max 200) |
| `--search-limit` | `ROSLYGATE_SEARCH_LIMIT` | `20` (max 50) |
| `--prewarm` / `--no-prewarm` | `ROSLYGATE_PREWARM` | `on` |
| `--check` | - | - |
| `-h, --help` | - | - |
| `-V, --version` | - | - |

Exit codes: `0` normal end of input or successful check, `1` invalid
configuration or solution, `2` fatal startup failure.

## Errors

Failures are self-correcting. When a call returns `isError: true`, read
`error.hint` and, if present, run `error.try`.

```json
{
  "error": {
    "code": "symbol_not_found",
    "message": "No symbol matches 'Widget.Opn'.",
    "hint": "Names are case-sensitive. Search by fragment instead.",
    "try": {
      "tool": "search_symbols",
      "arguments": { "query": "Opn", "limit": 10 }
    }
  }
}
```

## Development

```bash
dotnet restore --locked-mode
dotnet build RoslynGate.sln -warnaserror
dotnet test RoslynGate.sln
dotnet format RoslynGate.sln --verify-no-changes
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the trunk-based workflow.

## License

MIT. See [LICENSE](LICENSE).
