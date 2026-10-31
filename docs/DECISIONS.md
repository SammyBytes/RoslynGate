# Decisions

One line per decision. If a decision changes, change this file in the same
pull request.

| ID | Decision | Reason |
| --- | --- | --- |
| D-01 | Use `ModelContextProtocol` 2.3.x with `Microsoft.Extensions.Hosting`. | The official SDK owns framing, lifecycle, and source-generated tools. Hand-written JSON-RPC adds code without adding value. |
| D-02 | Three projects: `Engine`, `Mcp`, `Cli`. | The protocol layer is the SDK. `Engine` stays free of MCP types; `Mcp` stays free of Roslyn types. |
| D-03 | Four tools: definition, references, hierarchy, search. | Search is what replaces `grep`; without it an agent still explores by text. |
| D-04 | `detail=compact` (default) or `detail=verbose`. | Compact keeps list responses small; verbose exists for humans and edge cases. |
| D-05 | Load the solution lazily and prewarm in the background. | The MCP handshake must stay fast; MSBuild cost belongs to the first query, not to `initialize`. |
| D-06 | One solution per process, read-only. | No cache invalidation, no shared mutable state, no surprises. |
| D-07 | Publish self-contained single-file, no AOT in v1. | The MCP SDK supports AOT; `MSBuildWorkspace` does not. AOT stays a measured spike, not a promise. |
| D-08 | Every actionable error carries `code`, `hint`, and `try`. | An agent must be able to recover without reading external documentation. |
| D-09 | Caps and stable ordering everywhere. | Determinism is a feature; unbounded responses defeat the purpose of the tool. |
| D-10 | Flags and environment variables, no config file. | Unix composition and predictable precedence: flag > environment > default. |
| D-11 | Use `System.CommandLine` 2.0.x in `Cli` only. | Replaces hand-written parsing and help with the Microsoft standard, keeps environment precedence and limit clamping in `OptionValues`, and leaves `Engine` and `Mcp` untouched. |
| D-12 | Pin `global.json` to an SDK present in the installation used by MSBuild. | A mismatch makes the MSBuild build host print SDK discovery output, which can leak into `stdout`. |
| D-13 | Diagnostics use `ILogger<T>` to stderr; `Console.SetOut(Console.Error)` is the only protocol guard. | Keeps `stdout` clean without discarding dependency output, and avoids scattered `Console.Error` callbacks in `Engine`. |
| D-14 | Ship self-contained single-file binaries with content self-extract; the npm wrapper verifies SHA-256 and caches. | Avoids the MSBuild/AOT incompatibility while keeping installation to one file and one command. |
| D-15 | Use an empty host builder and ReadyToRun binaries to reduce startup; keep <50 ms as an AOT/low-level-host goal. | Warm handshake drops from ~1.0 s to ~0.32 s; the first run still pays single-file extraction (~1.4 s), and workspace loading stays deferred. |
| D-16 | Truncated reference and search pages carry `nextOffset` and an explicit follow-up hint. | Agents should not have to infer the next offset from `total`, `offset`, and page length; incomplete answers are worse than an extra call. |
| D-17 | Upgrades are explicit via `roslyngate upgrade`; there is no `postinstall`. | A package install must not silently replace binaries or rewrite `.mcp.json`; the command updates the global package, binary, and existing server entry only when the user asks. |
| D-18 | `find_symbol_references` can return the resolved definition via `includeDefinition=true`. | An agent that needs both pieces can make one call instead of two, reducing turns, latency, and repeated context. |
| D-19 | The project skill is opt-in via `setup claude --skill`, and upgrade only rewrites managed cache paths. | Teams can commit a shared skill without surprising existing setups, and portable or custom MCP commands are not replaced with machine-specific paths. |
| D-20 | The runtime is self-contained; the .NET SDK is an explicit prerequisite, selected by `global.json` when present. | MSBuild needs SDK targets and reference packs, and bundling one SDK would be huge and version-locked. Honoring `global.json` keeps each repository on its chosen SDK. |
| D-21 | The watcher is on by default and updates `.cs` files incrementally; structural changes set `stale` and require `reload_workspace`. | A full reload after every edit would cost ~27 s. Incremental updates keep the common edit-query loop fast while making stale results explicit. |
| D-22 | Overloads accept `parameterTypes` before file + line. | A type hint is less context and fewer turns than selecting a candidate by position. |
| D-23 | OpenCode setup writes `mcp.servers.roslyngate` with `codemode: false`. | It matches OpenCode V2's configuration shape and keeps RoslynGate tools native instead of wrapping them in Code Mode. |
| D-24 | OpenCode's strict skill is loaded through `instructions` and is opt-in. | A skill description alone does not force tool use; loading the text keeps the one-call/parameterTypes rules in context without changing Claude's setup. |
| D-25 | One monorepo, two npm packages (`roslyngate`, `roslyngate-ts`). | A contract break must fail CI across both products, while neither runtime downloads the other's payload. |
| D-26 | JSON Schema in `contracts/schema` is the source of truth; TS types and C# DTOs are generated. | Hand-maintained parallel shapes drift silently; `contracts:check` makes drift fail before release. |
| D-27 | The TS server uses the low-level MCP `Server` with raw JSON Schema. | High-level `McpServer`/zod changes schema rendering; raw definitions guarantee exact `tools/list` parity. |
| D-28 | `version.json` holds the release, contract, and per-product versions; `eng/check-version` enforces lockstep. | One tag must publish both products now, while leaving room for independent product versions later. |
| D-29 | Each product autodetects its project type and points to the other package on mismatch. | Installing the wrong package should produce one actionable instruction instead of a solution-load failure. |
| D-30 | `roslyngate-ts upgrade` refreshes managed MCP entries only. | Upgrades must not rewrite custom commands, but they should remove stale `npx` cache paths. |
