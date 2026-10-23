# Published E2E

Checks a real release binary or the npm wrapper over MCP stdio.

```bash
python3 tests/published/e2e.py \
  --command /path/to/roslyngate-linux-x64 \
  --solution fixtures/SampleSolution/SampleSolution.sln

python3 tests/published/e2e.py \
  --command "npx -y roslyngate@0.1.8" \
  --solution fixtures/SampleSolution/SampleSolution.sln
```

It verifies:

- `--version` matches the expected release.
- `tools/list` exposes exactly the four tools.
- Definition, references, hierarchy, and search return valid content.
- No tool returns `isError: true`.

This was run on Linux x64. Windows and macOS binaries are built by the release
workflow but still need a smoke run on those operating systems.
