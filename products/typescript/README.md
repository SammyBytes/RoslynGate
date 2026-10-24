# roslyngate-ts

TypeScript/JavaScript semantic MCP server. This is the MVP vertical slice: the
engine, five tools, hierarchy, and a fixture test work; npm packaging and
contract parity are tracked in `docs/TODO-TS.md`.

```bash
npm install
npm run build
node dist/cli.js --check --solution ../../fixtures/TsSample
```

Setup:

```bash
node dist/cli.js setup opencode --solution /path/to/project --skill
node dist/cli.js setup claude --solution /path/to/project --skill
```
