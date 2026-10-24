# roslyngate

npm wrapper for [RoslynGate](https://github.com/SammyBytes/RoslynGate), a
token-efficient MCP server for .NET symbol lookup.

## Usage

Install and configure Claude Code in one step:

```bash
npm install -g roslyngate
cd /path/to/repository
roslyngate setup claude
```

Or run it without installing:

```bash
npx -y roslyngate@0.2.2 setup claude
bunx roslyngate@0.2.2 setup claude
```

`setup claude` writes or merges `.mcp.json` with the native binary path and
solution path. Add `--skill` to also write `.claude/skills/roslyn-symbols/SKILL.md`:

```bash
roslyngate setup claude --skill
```

The skill is opt-in and an existing file is left unchanged. To configure
OpenCode instead:

```bash
roslyngate setup opencode --solution . --skill
```

This writes or merges `opencode.jsonc` with the server under
`mcp.servers.roslyngate`, disables Code Mode, and adds the strict
`.opencode/skills/roslyn-symbols` skill to `instructions`.

To use the CLI directly:

```bash
roslyngate --solution /path/to/repository
```

The first run downloads the self-contained binary for the current platform,
verifies its SHA-256 checksum, and caches it under `~/.cache/roslyngate` on
Linux/macOS or `%LOCALAPPDATA%\roslyngate` on Windows.

Bun consumes npm packages from the npm registry. `bunx roslyngate` works
without a separate Bun package or registry publish.

## Upgrade

```bash
roslyngate upgrade
```

This updates the global npm package, downloads the matching binary, and
refreshes a RoslynGate-managed `roslyngate.command` path in `.mcp.json` in the
current directory. It preserves the existing arguments, timeout, and other MCP
servers. Portable commands such as `roslyngate` or `npx` are left alone.
Upgrade detects npm or Bun from the active command; override with
`--manager npm|bun`. Use `--no-config` to update only the package and binary,
`--check` to compare versions, or `--dry-run` to preview the actions.

## Supported platforms

- Linux x64
- Windows x64
- macOS arm64

Unsupported platforms fail with a clear message instead of a stack trace.

The package version is checked against `<Version>` in
`Directory.Build.props`; `npm test` fails if they drift.

## Environment

| Variable | Purpose |
| --- | --- |
| `ROSLYGATE_VERSION` | Override the release version. |
| `ROSLYGATE_CACHE_DIR` | Override the cache directory. |
| `ROSLYGATE_BASE_URL` | Override the release download base URL. |
| `ROSLYGATE_BINARY` | Use an existing binary and skip the download. |
| `ROSLYGATE_REGISTRY` | Override the npm registry used by `upgrade`. |

`ROSLYGATE_BINARY` is useful for local development:

```bash
ROSLYGATE_BINARY=/path/to/roslyngate npx roslyngate --check
```

## Test

```bash
npm test
```

The tests use fake fetches and temporary directories; no network is required.
