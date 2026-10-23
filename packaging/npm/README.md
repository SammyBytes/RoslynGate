# roslyngate

npm wrapper for [RoslynGate](https://github.com/SammyBytes/RoslynGate), a
token-efficient MCP server for .NET symbol lookup.

## Usage

Install the wrapper from npm:

```bash
npm install -g roslyngate
roslyngate --solution /path/to/repository
```

Or run it without installing:

```bash
npx -y roslyngate --solution /path/to/repository
bunx roslyngate --solution /path/to/repository
```

The first run downloads the self-contained binary for the current platform,
verifies its SHA-256 checksum, and caches it under `~/.cache/roslyngate` on
Linux/macOS or `%LOCALAPPDATA%\roslyngate` on Windows.

Bun consumes npm packages from the npm registry. `bunx roslyngate` works
without a separate Bun package or registry publish.

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

`ROSLYGATE_BINARY` is useful for local development:

```bash
ROSLYGATE_BINARY=/path/to/roslyngate npx roslyngate --check
```

## Test

```bash
npm test
```

The tests use fake fetches and temporary directories; no network is required.
