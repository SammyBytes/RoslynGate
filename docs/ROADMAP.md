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
- [ ] Validate restore, build, test, and format.
- [ ] Create the public repository and push the bootstrap commit.
- [ ] Configure merge settings and protect `main`.

## F0 - MCP host

- [ ] Build the stdio host with the official SDK.
- [ ] Add `--help`, `--version`, and `--check`.
- [ ] Verify that every stdout line is valid JSON-RPC.
- [ ] Configure logging to stderr only.

## F1 - Workspace

- [ ] Add the sample solution fixture.
- [ ] Implement solution discovery with actionable errors.
- [ ] Implement lazy loading and single-flight.
- [ ] Add background prewarm and cancellation.
- [ ] Test 0, 1, and 2 solutions.

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
- [ ] Publish `v0.1.0` with checksums.
- [ ] Run the AOT spike and record the result.
