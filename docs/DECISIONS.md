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
