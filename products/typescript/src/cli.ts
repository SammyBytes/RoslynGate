#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import ts from "typescript";
import { TsWorkspace, type ReferenceInfo, type SymbolInfo } from "./workspace.js";
import { runSetup } from "./setup.js";

const VERSION = "0.3.0";

function parseArgs(argv: string[]) {
  let root = process.cwd();
  let config: string | undefined;
  let check = false;
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--solution" || arg === "--project") {
      root = argv[++i] ?? root;
    } else if (arg === "--config") {
      config = argv[++i];
    } else if (arg === "--check") {
      check = true;
    } else if (arg === "--no-watch") {
      process.env.ROSLYGATE_TS_WATCH = "off";
    } else if (arg === "--version" || arg === "-v") {
      process.stdout.write(`${VERSION}\n`);
      process.exit(0);
    } else if (arg === "--help" || arg === "-h") {
      process.stdout.write("Usage: roslyngate-ts [--solution <dir>] [--config <tsconfig>] [--check] [--no-watch]\n");
      process.exit(0);
    }
  }
  return { root, config, check };
}

function compact(info: SymbolInfo) {
  return {
    n: info.name,
    q: info.qualifiedName,
    k: info.kind,
    s: info.signature,
    f: info.location.file,
    l: info.location.line
  };
}

function withState(value: Record<string, unknown>, workspace: TsWorkspace): Record<string, unknown> {
  const state = workspace.state();
  if (!state.stale) return value;
  return {
    ...value,
    stale: true,
    changed: state.changed,
    hint: "The workspace changed after it was loaded. Call reload_workspace, then retry.",
    try: { tool: "reload_workspace", arguments: {} }
  };
}

function text(value: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(value) }] };
}

function error(code: string, message: string, hint: string) {
  return { isError: true, content: [{ type: "text" as const, text: JSON.stringify({ error: { code, message, hint } }) }] };
}

async function main() {
  if (process.argv[2] === "setup") {
    const target = process.argv[3];
    if (target !== "claude" && target !== "opencode") {
      throw new Error("Usage: roslyngate-ts setup claude|opencode [--solution <path>] [--skill] [--dry-run]");
    }
    runSetup(target, process.argv.slice(4));
    return;
  }

  const args = parseArgs(process.argv.slice(2));
  const workspace = new TsWorkspace(args.root, args.config);

  try {
    workspace.load();
  } catch (cause) {
    const message = cause instanceof Error ? cause.message : String(cause);
    if (args.check) {
      process.stdout.write(`roslyngate-ts: ${message}\nstatus       error\n`);
      process.exit(1);
    }
    serverFailure(message);
    return;
  }

  if (args.check) {
    process.stdout.write(`roslyngate-ts ${VERSION}\nconfig       ${workspace.configPath}\nroot         ${workspace.root}\nfiles        ${workspace.sourceFileCount()}\nwatch        ${process.env.ROSLYGATE_TS_WATCH === "off" ? "off" : "on"}\nstatus       ok\n`);
    workspace.close();
    return;
  }

  const server = new McpServer({ name: "roslyn-gate-ts", version: VERSION });
  const staleHint = "Call reload_workspace and retry.";

  server.registerTool(
    "get_symbol_definition",
    {
      description: "Resolve a TypeScript/JavaScript symbol by qualified name or file and 1-based line.",
      inputSchema: {
        name: z.string().optional(),
        file: z.string().optional(),
        line: z.number().int().optional(),
        column: z.number().int().optional(),
        parameterTypes: z.array(z.string()).optional()
      }
    },
    async ({ name, file, line, column, parameterTypes }) => {
      const matches = filterOverloads(workspace.resolve(name, file, line, column), parameterTypes);
      if (matches.length === 0) return text({ symbol: null, hint: "Use search_symbols to find a fragment." });
      if (matches.length > 1) return text({ ambiguous: true, candidates: matches.slice(0, 20).map(compact), hint: "Use parameterTypes or file + line." });
      return text(withState(compact(matches[0]), workspace));
    }
  );

  server.registerTool(
    "find_symbol_references",
    {
      description: "Find semantic references to a TypeScript/JavaScript symbol, with deterministic paging.",
      inputSchema: {
        name: z.string().optional(),
        file: z.string().optional(),
        line: z.number().int().optional(),
        column: z.number().int().optional(),
        parameterTypes: z.array(z.string()).optional(),
        includeDefinition: z.boolean().optional(),
        offset: z.number().int().optional(),
        limit: z.number().int().optional()
      }
    },
    async ({ name, file, line, column, parameterTypes, includeDefinition, offset, limit }) => {
      const matches = filterOverloads(workspace.resolve(name, file, line, column), parameterTypes);
      if (matches.length === 0) return text({ q: null, total: 0, refs: [] });
      if (matches.length > 1) return text({ ambiguous: true, candidates: matches.slice(0, 20).map(compact), hint: "Use parameterTypes or file + line." });
      const symbol = matches[0];
      const page = workspace.references(symbol, offset ?? 0, Math.min(limit ?? 50, 200));
      const result: Record<string, unknown> = {
        q: symbol.qualifiedName,
        total: page.total,
        offset: offset ?? 0,
        truncated: page.truncated,
        refs: page.refs.map((ref: ReferenceInfo) => [ref.file, ref.line, ref.kind])
      };
      if (includeDefinition) result.definition = compact(symbol);
      if (page.truncated) {
        const next = (offset ?? 0) + page.refs.length;
        result.nextOffset = next;
        result.hint = `Call find_symbol_references again with offset=${next} and the same arguments.`;
      }
      return text(withState(result, workspace));
    }
  );

  server.registerTool(
    "get_type_hierarchy",
    {
      description: "Return base types, interfaces, derived types, and implementations for a TS type.",
      inputSchema: {
        name: z.string().optional(),
        file: z.string().optional(),
        line: z.number().int().optional(),
        column: z.number().int().optional()
      }
    },
    async ({ name, file, line, column }) => {
      const matches = workspace.resolve(name, file, line, column);
      if (matches.length === 0) return text({ type: null });
      if (matches.length > 1) return text({ ambiguous: true, candidates: matches.slice(0, 20).map(compact) });
      return text(withState({ type: compact(matches[0]), ...workspace.hierarchy(matches[0]) }, workspace));
    }
  );

  server.registerTool(
    "search_symbols",
    {
      description: "Search TypeScript/JavaScript types and members by name fragment.",
      inputSchema: {
        query: z.string(),
        offset: z.number().int().optional(),
        limit: z.number().int().optional()
      }
    },
    async ({ query, offset, limit }) => {
      if (query.trim().length < 2) return error("query_too_short", "Search query must contain at least 2 characters.", "Use a fragment such as Widget or Open.");
      const page = workspace.search(query, offset ?? 0, Math.min(limit ?? 20, 50));
      return text(withState({
        total: page.total,
        offset: offset ?? 0,
        truncated: page.truncated,
        hits: page.hits.map((hit) => [hit.qualifiedName, hit.kind, hit.location.file, hit.location.line])
      }, workspace));
    }
  );

  server.registerTool(
    "reload_workspace",
    {
      description: "Reload the tsconfig/Program after structural file changes."
    },
    async () => {
      workspace.reload();
      return text({ status: "reloaded", files: workspace.sourceFileCount() });
    }
  );

  await server.connect(new StdioServerTransport());
  process.on("SIGINT", () => process.exit(0));
  process.on("SIGTERM", () => process.exit(0));
  if (process.env.ROSLYGATE_TS_STALE === "1") process.stderr.write(`${staleHint}\n`);
}

function filterOverloads(matches: SymbolInfo[], parameterTypes?: string[]): SymbolInfo[] {
  if (!parameterTypes || parameterTypes.length === 0) return matches;
  return matches.filter((match) => {
    const declaration = match.declaration as ts.SignatureDeclaration;
    if (!declaration.parameters || declaration.parameters.length !== parameterTypes.length) {
      return false;
    }
    return declaration.parameters.every((param, index) => {
      const actual = param.type?.getText(param.getSourceFile()) ?? "unknown";
      const expected = parameterTypes[index];
      return actual === expected
        || actual.endsWith(`.${expected}`)
        || actual.replaceAll(" ", "") === expected.replaceAll(" ", "");
    });
  });
}

function serverFailure(message: string) {
  process.stderr.write(`roslyngate-ts: ${message}\n`);
  process.exit(1);
}

main().catch((cause) => serverFailure(cause instanceof Error ? cause.message : String(cause)));
