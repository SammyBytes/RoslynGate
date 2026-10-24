# Roadmap

## Environment

- [x] Install .NET 10 SDK and verify `dotnet --version`.
- [x] Configure global identity to `SammyBytes <samuelbeato7@proton.me>`.
- [x] Configure repository identity to the GitHub noreply address.
- [x] Remove the stale `SammyBits` GitHub CLI account.
- [x] Create solution, three projects, and three test projects.
- [x] Add build props, package pinning, NuGet config, and editor config.
- [x] Add README, CONTRIBUTING, SECURITY, LICENSE, decisions, roadmap.
- [x] Add CI, PR title lint, pull request template, and Dependabot.
- [x] Validate restore, build, test, and format.
- [x] Create the public repository and push the bootstrap commit.
- [x] Configure merge settings and protect `main`.
- [x] Confirm the environment before starting F0.

## F0 - MCP host

- [x] Build the stdio host with the official SDK.
- [x] Add `--help`, `--version`, and `--check`.
- [x] Verify that every stdout line is valid JSON-RPC.
- [x] Configure logging to stderr only.
- [x] Document MCP client setup for Claude Code, Cursor, VS Code, and a
      generic `mcpServers` JSON block.
- [x] Add the `opencode.jsonc` example and verify the connection.
- [x] Document `--check` and the stdout/stderr split for troubleshooting.
- [x] Replace the hand-written parser with `System.CommandLine`.
- [x] Use `ILogger` for diagnostics, guard `stdout` at the transport boundary,
      and extract shared names, limits, and exit codes.

## F1 - Workspace

- [x] Add the sample solution fixture.
- [x] Implement solution discovery with actionable errors.
- [x] Implement lazy loading and single-flight.
- [x] Add background prewarm and cancellation.
- [x] Test 0, 1, and 2 solutions.
- [x] Make `--check` load the solution and report projects and load time.

## F2 - Definition

- [x] Resolve by qualified name.
- [x] Resolve by file and line.
- [x] Return deterministic candidates for ambiguity.
- [x] Serialize compact and verbose.
- [x] Expose `get_symbol_definition` over MCP with actionable errors.

## F3 - References and hierarchy

- [x] Find semantic references with stable order.
- [x] Paginate with `offset`, `limit`, and `total`.
- [x] Return base types, interfaces, derived types, and implementations.
- [x] Enforce the token budget.
- [x] Expose `find_symbol_references` and `get_type_hierarchy` over MCP.
- [x] Return the resolved definition from `find_symbol_references` when
      `includeDefinition=true`.
- [x] Disambiguate overloads with `parameterTypes`.

## F4 - Search and errors

- [x] Implement `search_symbols` with deterministic ranking.
- [x] Add the error catalog with `hint` and `try`.
- [x] Add `nextOffset` and a follow-up hint when a reference or search page is
      truncated.
- [x] Add budget and error catalog tests.
- [x] Expose `search_symbols` over MCP with kind and namespace filters.

## F5 - Distribution

- [x] Add the release workflow for linux-x64, win-x64, and osx-arm64.
- [x] Add the npm wrapper with checksum verification and cache.
- [x] Publish the npm wrapper (`roslyngate@0.2.0`).
- [x] Add `roslyngate setup claude` with `--print` and `--dry-run`.
- [x] Add opt-in `setup claude --skill` for a committable project skill.
- [x] Add `roslyngate upgrade` with `--check`, `--dry-run`, and `--no-config`;
      refresh only managed binary paths and preserve the rest of `.mcp.json`.
- [x] Auto-detect npm or Bun for `upgrade` and allow `--manager` to override.
- [x] Add `reload_workspace`, `stale`/`file_not_indexed`, and a default-on
      incremental watcher for `.cs` files.
- [x] Honor `global.json` when selecting the .NET SDK and show it in `--check`.
- [x] Fail with an actionable hint when the required SDK is not installed.
- [x] Document installation paths and `npx` usage.
- [x] Document flags, environment variables, defaults, and output modes.
- [x] Measure token volume and latency against `rg`.
- [x] Measure real Claude Code token usage with and without RoslynGate on a
      99-project legacy solution (three-symbol and six-symbol runs).
- [x] Repeat the Claude Code A/B with a broader symbol set and compare answer
      completeness.
- [x] Document per-prompt Claude Code A/B with `claude-ab.ps1 -Prompt`.
- [ ] Investigate the remaining missing references: 4 of 98 in the three-symbol
      run and 2 of 157 in the six-symbol run.
- [x] Publish `v0.1.0` with binaries and checksums.
- [x] Reduce startup with an empty host builder and ReadyToRun.
- [ ] Wait for explicit approval before the AOT spike. If approved, run it on a
      separate experimental branch and record the result.

## F6 - Release

- [x] Write a formal `CHANGELOG.md` with plain-language summaries.
- [x] Publish GitHub Releases for `v0.1.0` and `v0.1.1` with changelog bodies.
- [x] Run published E2E on Linux x64 against the release binary.
- [x] Run published E2E on Linux x64 through `npx roslyngate@0.2.0`.
- [x] Run published E2E on Linux x64 against the `v0.2.0` release binary.
- [x] Verify `roslyngate setup claude` against the published npm package.
- [ ] Smoke-test the Windows and macOS binaries on their platforms.
