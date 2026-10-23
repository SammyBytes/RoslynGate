# Changelog

All notable changes to RoslynGate are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.8] - 2026-10-23

**In plain terms:** before loading a solution, RoslynGate now checks for a
usable .NET SDK and shows which one it selected. If `global.json` requires an
SDK that is not installed, it says exactly what is missing instead of crashing
or silently using a different SDK.

### Added

- Resolve the SDK with `dotnet --version` and `dotnet --list-sdks`, honoring
  `global.json`.
- Register MSBuild from the selected SDK path, avoiding the `hostfxr` lookup
  failure in self-contained single-file builds.
- Show `sdk` and `sdk-path` in `--check`.
- Return an actionable hint when no SDK, or the SDK pinned by `global.json`, is
  installed.

### Fixed

- Include native libraries in the single-file Windows build.
- Report SDK/MSBuild registration failures as workspace errors instead of raw
  stack traces.

### Tests

- Cover missing pinned SDKs and the SDK information shown by `--check`.

## [0.1.7] - 2026-10-23

**In plain terms:** `roslyngate upgrade` now detects whether the active command
was installed with npm or Bun, so it updates the installation you actually use.
It also removes the Windows `DEP0190` warning and accepts `-v` as a version
alias.

### Fixed

- Detect Bun from the global install path and run `bun install -g` instead of
  npm.
- Add `--manager npm|bun` for an explicit override.
- Run npm through `cmd.exe` on Windows without `shell: true`, removing the
  `DEP0190` warning.
- Accept `-v` as an alias for `--version`.

### Tests

- Cover Bun detection, Bun installs, the Windows npm path, and the manager
  option.

## [0.1.6] - 2026-10-23

**In plain terms:** RoslynGate now returns the definition and references in one
call when asked, can install a committable Claude Code skill, and upgrades only
touch an MCP config that RoslynGate manages. A new `--no-config` option updates
the package and binary without touching any project files.

### Added

- Add `includeDefinition=true` to `find_symbol_references`, returning the
  resolved symbol in the same response.
- Add opt-in `roslyngate setup claude --skill`, which writes a project skill
  under `.claude/skills/roslyn-symbols/` and leaves an existing skill alone.
- Add `roslyngate upgrade --no-config`.
- Document per-prompt Claude Code A/B with `claude-ab.ps1 -Prompt`.

### Changed

- `roslyngate upgrade` now rewrites `roslyngate.command` only when it points to
  a RoslynGate-managed cache path. Portable commands such as `roslyngate` or
  `npx` are left unchanged.

### Tests

- Cover `includeDefinition` in compact and E2E responses, the opt-in skill,
  managed versus portable command detection, and `--no-config`.

## [0.1.5] - 2026-10-23

**In plain terms:** `roslyngate upgrade` updates the global npm package, gets
the matching native binary, and points the current `.mcp.json` at it. One
command keeps the wrapper, the binary, and Claude Code in sync.

### Added

- Add `roslyngate upgrade`, with `--check` and `--dry-run`.
- Preserve the existing `args`, `timeout`, and other MCP servers while updating
  only the `roslyngate.command` path.
- Print a setup hint when the current directory has no `.mcp.json`.

### Tests

- Cover version comparison, latest-version lookup, installs, config updates,
  and the read-only `--check` path.

## [0.1.4] - 2026-10-23

**In plain terms:** when a reference or search result has more items than fit
on one page, RoslynGate now returns the exact offset for the next page and a
hint that tells the agent how to ask for it. Agents no longer have to calculate
the next call or stop with an incomplete answer.

### Added

- Return `nextOffset` and an actionable `hint` when `find_symbol_references` or
  `search_symbols` truncates a page.
- Cover compact references, verbose search, and non-truncated responses with
  tests.

### Fixed

- Fix `benchmarks/claude-ab.ps1` so the MCP config parameter is not overwritten
  by a case-insensitive local variable, and write the baseline config as UTF-8
  without a BOM.
- Record per-run input, output, cache, turn, and duration data in the Claude
  A/B benchmark, and keep each raw JSON response for inspection.
- Restrict built-in tools with `--tools` so the `with` run cannot silently use
  `Read`, `Grep`, or `Glob` alongside RoslynGate.

## [0.1.3] - 2026-10-23

**In plain terms:** RoslynGate no longer refuses to open a large solution just
because some old packages print compatibility warnings. It opens the projects
it can load, prints those warnings, and lets you use the symbol tools.

### Fixed

- Accept MSBuild diagnostics when the solution still loads at least one project.
- Report those diagnostics as warnings in `--check` and through `ILogger`.
- Fail only when the solution produces no projects at all.

### Tests

- Workspace, CLI check, and published E2E suites remain green.
- The change is designed for legacy solutions mixing .NET Framework packages
  with modern target frameworks.

## [0.1.2] - 2026-10-23

**In plain terms:** setup for Claude Code is now one command. It downloads the
right program for your computer, writes the project configuration, and avoids
making you copy paths or type `npx` commands.

### Added

- `roslyngate setup claude` writes or merges `.mcp.json`.
- `--solution`, `--print`, and `--dry-run` options.
- The generated config uses the cached native binary path, so Claude Code does
  not need `npx` or `cmd /c`.
- Existing MCP servers and top-level configuration keys are preserved.

### Tests

- Setup parser, path resolution, config merge, malformed config, and write
  behavior are covered by wrapper tests.
- `npm test` also checks the package version against `Directory.Build.props`.

## [0.1.1] - 2026-10-23

**In plain terms:** RoslynGate now opens noticeably faster after the first run.
The very first launch is still slower because the program has to unpack itself,
and the first question still waits for the project to load, but repeat launches
no longer pay that full cost.

### Changed

- Use `Host.CreateEmptyApplicationBuilder` to skip unused host configuration.
- Publish ReadyToRun binaries for faster startup.
- Warm MCP handshake improved from about 1.0 s to about 0.33 s on Linux x64.
- Bump the package and binary version to `0.1.1`.

### Tests

- 78 .NET tests and 12 wrapper tests pass.
- Measured token and latency results are published in `docs/BENCHMARKS.md`.
- Verified `npx -y roslyngate@0.1.1` and `bunx roslyngate@0.1.1` return `0.1.1`.
- Verified the published Linux binary and npm wrapper end-to-end over MCP.

## [0.1.0] - 2026-10-23

**In plain terms:** this is the first public release of RoslynGate. It lets an
AI assistant ask a .NET project where a symbol is defined, who uses it, and how
types are related, instead of reading the whole codebase with text search.

### Added

- `get_symbol_definition` by qualified name or file and 1-based line.
- `find_symbol_references` with semantic references and paging.
- `get_type_hierarchy` with base types, interfaces, derived types, and
  implementations.
- `search_symbols` with exact, prefix, and contains ranking.
- Compact and verbose JSON output with token caps.
- Self-contained binaries for Linux x64, Windows x64, and macOS arm64.
- npm wrapper with SHA-256 verification and binary cache.
- OpenCode, Claude Code, Cursor, and VS Code configuration examples.
- Reproducible `rg` comparison benchmark.

### Tests

- 78 .NET tests and 12 wrapper tests pass.
- Release assets and checksums were generated by the release workflow.
- Benchmarks show 76-92% fewer characters than `rg` for the measured tasks.
