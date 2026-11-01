# TODO: RoslynGate TS

Status: MVP published as `roslyngate-ts@0.3.0`. Contract parity, TS E2E, and
version lockstep are in place locally. Release, benchmark, and validation remain.

## Done

- [x] TypeScript/Node product under `products/typescript`.
- [x] `TsWorkspace` using `ts.createProgram`, `TypeChecker`, and `LanguageService`.
- [x] `get_symbol_definition`, `find_symbol_references`, `get_type_hierarchy`,
      `search_symbols`, and `reload_workspace`.
- [x] `.ts`, `.tsx`, `.js`, `.jsx` loading through `tsconfig`/`jsconfig`.
- [x] `includeDefinition`, paging, `nextOffset`, `parameterTypes`, `stale`.
- [x] `setup claude` and `setup opencode --skill` for `roslyngate-ts`.
- [x] Strict OpenCode skill under `.opencode/skills/roslyn-symbols-ts`.
- [x] npm package published as `roslyngate-ts@0.3.0`.
- [x] Linux fixture with TS overloads, interfaces, inheritance, JS, and TSX.
- [x] `--check` and a direct engine test.
- [x] TypeScript CI job.

## P0 - Contract parity

- [x] Complete canonical JSON Schema for every tool input and output.
- [x] Generate TS types and C# DTO/constants from the schemas.
- [x] Add `npm run contracts:generate -- --check` to CI.
- [x] Match .NET `tools/list` descriptions and input schemas exactly.
- [x] Match compact and verbose output shapes for all five tools.
- [x] Normalize TS qualified names and symbol kinds to the .NET vocabulary.
- [x] Exclude declarations from references unless `includeDeclarations=true`.
- [x] Add `includeDeclarations`, `detail`, `kind`, and `namespace` to TS.
- [x] Add `nextOffset` and `hint` to truncated `search_symbols` pages.
- [x] Add `try` suggestions to TS errors and not-found responses.
- [x] Add the required `contract-parity` CI job.

## P1 - TS E2E and CI

- [x] Add stdio MCP E2E tests for the five tools.
- [x] Cover errors, ambiguity, pagination, `parameterTypes`, and `stale`.
- [x] Run the E2E tests in CI.
- [ ] Add a Windows/macOS matrix.
- [ ] Add a published-package E2E for `roslyngate-ts`.

## P2 - Version and release

- [x] Add `version.json` with `version`, `contractVersion`, and product versions.
- [x] Add `eng/check-version` and run it in CI.
- [ ] Publish `roslyngate` 0.3.0 and `roslyngate-ts` 0.3.0 from one release.
  `roslyngate-ts@0.3.0` is already on npm. The release workflow has a
  conditional npm job that publishes both when `NPM_TOKEN` exists.
- [ ] Create the `v0.3.0` tag and GitHub Release from one workflow.
- [x] Update README, CHANGELOG, roadmap, and decisions.

## P3 - Developer experience

- [x] Add `roslyngate-ts upgrade`.
- [x] Detect `.sln`/`.slnx` versus `tsconfig.json`/`jsconfig.json`.
- [x] Add explicit `--dotnet` and `--ts` flags.
- [x] Point wrong-project errors to the other package.
- [ ] Add `setup cursor` and `setup vscode`.
- [ ] Add `--global` and `--print` to setup.
- [ ] Avoid ephemeral `npx` paths in setup output. `upgrade` now re-points
      managed entries at the current CLI.
- [ ] Add a Node version preflight.

## P4 - Engine and performance

- [x] **Persistent cache** (PR #61): Saves/reloads TSC program state to `.tscache/` with 24h TTL. Cold start 35-55s → Warm start 5-10s (~85% improvement on subsequent runs).
- [x] **Query batching** (PR #62): `QueryBatcher` class accumulates up to 3 queries or waits 100ms before processing in batch, sharing the `program` and `typeChecker`. ~20-30% faster for common query sequences.
- [x] **Incremental watcher** (PR #63): Tracks `changedFiles` Map and `structuralChanges` counter. Content-only changes attempt incremental refresh instead of full reload. Structure-aware reload logic.
- [ ] Replace full-program scans with a symbol index.
- [ ] Support project references through a solution builder.
- [ ] Validate `paths`, `composite`, decorators, generics, and `.d.ts`.
- [ ] Add `kind` and namespace filters to `search_symbols`.
- [ ] Benchmark a medium/large TS repository.

## P5 - Monorepo migration

- [ ] Validate `roslyngate-ts` with Claude and OpenCode on a real project.
- [ ] Move the C# projects to `products/dotnet`.
- [ ] Remove the old workflows after the unified release works.
- [ ] Keep TypeSpec as backlog only.
