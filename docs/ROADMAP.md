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

- [ ] Resolve by qualified name.
- [ ] Resolve by file and line.
- [ ] Return deterministic candidates for ambiguity.
- [ ] Serialize compact and verbose.

## F3 - References and hierarchy

- [ ] Find semantic references with stable order.
- [ ] Paginate with `offset`, `limit`, and `total`.
- [ ] Return base types, interfaces, derived types, and implementations.
- [ ] Enforce the token budget.

## F4 - Search and errors

- [ ] Implement `search_symbols` with deterministic ranking.
- [ ] Add the error catalog with `hint` and `try`.
- [ ] Add budget and error catalog tests.

## F5 - Distribution

- [ ] Publish self-contained binaries for three RIDs.
- [ ] Add the npm wrapper.
- [ ] Document installation paths and `npx`/`bunx` usage.
- [ ] Document every flag, environment variable, default, and output mode
      with copyable examples.
- [ ] Publish `v0.1.0` with checksums.
- [ ] Wait for explicit approval before the AOT spike. If approved, run it on a
      separate experimental branch and record the result.
