# Benchmarks

`compare.py` compares RoslynGate tool output and latency with `rg` baselines.

```bash
dotnet publish src/RoslynGate.Cli/RoslynGate.Cli.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o artifacts/bench

python3 benchmarks/compare.py \
  --binary artifacts/bench/roslyngate \
  --solution RoslynGate.sln \
  --repo .
```

The script:

1. Starts the MCP server over stdio without prewarm.
2. Measures handshake, first tool call, and warm tool-call latency.
3. Runs equivalent `rg` commands and counts output characters.
4. Estimates tokens as `chars / 4`.
5. Reads peak RSS from `/proc/<pid>/status` on Linux.

Results are committed in [docs/BENCHMARKS.md](../docs/BENCHMARKS.md). The script
is not part of CI because it is slow and machine-dependent.

## Claude Code A/B on Windows

`claude-ab.ps1` compares the **real token usage** reported by Claude Code with
and without RoslynGate. It requires a logged-in `claude` CLI and a `.mcp.json`
created by `roslyngate setup claude`.

```powershell
.\benchmarks\claude-ab.ps1 `
  -Symbols "SampleSdk.Widget","SampleSdk.SecureWidget","SampleSdk.IOpenable" `
  -Runs 3
```

Use symbols that exist in your repository. For a custom task:

```powershell
.\benchmarks\claude-ab.ps1 -Prompt "Find the definition and references of ..." -Runs 3
```

The script:

1. Copies the MCP config and creates an empty `mcpServers` config.
2. Runs `claude -p --output-format json --strict-mcp-config`.
3. Alternates with/without order to reduce cache bias.
4. Reads `usage`, `total_cost_usd`, and prints the median token reduction.
5. Saves raw JSON and stderr under a temporary directory.

A single small question often shows no gain because the four tool schemas and
server instructions add fixed context. Use several symbols and a real repository
to measure the net effect.

If Claude Code reports an error about a missing `usage` field, inspect the raw
JSON printed by the script; the field layout varies by Claude Code version.
