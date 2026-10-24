# TODO: RoslynGate TS

Status: MVP vertical slice implemented; release and C# migration remain.

## Done

- [x] TypeScript/Node product skeleton under `products/typescript`.
- [x] `TsWorkspace` using `ts.createProgram`, `TypeChecker`, and `LanguageService`.
- [x] `get_symbol_definition`, `find_symbol_references`, `get_type_hierarchy`,
      `search_symbols`, and `reload_workspace`.
- [x] `.ts`, `.tsx`, `.js`, `.jsx` loading through `tsconfig`/`jsconfig`.
- [x] `includeDefinition`, paging, `nextOffset`, `parameterTypes`, `stale`.
- [x] Linux fixture with TS overloads, interfaces, inheritance, JS, and TSX.
- [x] `--check` and a direct engine test.

## Next

- [ ] `roslyngate-ts setup claude` and `setup opencode --skill`.
- [ ] strict OpenCode skill for the TS product.
- [ ] package `roslyngate-ts` for npm with compiled `dist`.
- [ ] contract JSON Schema and generated TS/C# types.
- [ ] contract-parity test between C# and TS `tools/list`.
- [ ] TS E2E through stdio MCP.
- [ ] watcher/incremental `LanguageService` updates.
- [ ] benchmark TS with OpenCode/Claude on a medium/large repo.
- [ ] version manifest `0.3.0` and single release workflow.
- [ ] move C# to `products/dotnet` after TS MCP is validated.
- [ ] TypeSpec evaluation backlog.
