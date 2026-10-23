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
