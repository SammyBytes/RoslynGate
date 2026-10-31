#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { TOOL_DEFINITIONS } from "./generated/tools.js";
import { ProjectFailure, detectTypeScriptProject } from "./project.js";
import { runSetup } from "./setup.js";
import { runUpgrade } from "./upgrade.js";
import {
  TsWorkspace,
  matchesParameterTypes,
  type SymbolInfo
} from "./workspace.js";

const VERSION = "0.3.0";
const AMBIGUOUS_HINT = "Use parameterTypes to choose an overload, or file + line from one candidate.";
const STALE_HINT = "The workspace changed after it was loaded. Call reload_workspace, then retry.";
const NOT_FOUND_HINT = "Use search_symbols to find a fragment.";

const INSTRUCTIONS = `RoslynGate answers TypeScript and JavaScript symbol questions with the TypeScript compiler, not text search.
Use get_symbol_definition to resolve one symbol, find_symbol_references to locate semantic uses, get_type_hierarchy for inheritance, and search_symbols when you only know a name fragment.

For an exact name that needs both definition and references, call find_symbol_references with includeDefinition=true instead of calling get_symbol_definition and find_symbol_references separately.

If overloads are ambiguous, pass parameterTypes (for example ["string", "int"]) instead of selecting by file and line.

A target is either a qualified name or "file" plus a 1-based "line". Prefer detail=compact. It omits namespace, containing type, and column unless detail=verbose is requested.

If a page is truncated, call the same tool again with offset=nextOffset. If a response has stale=true, do not treat empty results as confirmed; call reload_workspace and retry. If a call returns isError, read error.hint and run error.try when present.`;

type ToolResult = {
  content: Array<{ type: "text"; text: string }>;
  isError?: boolean;
};

type Arguments = Record<string, unknown>;

async function main() {
  if (process.argv[2] === "upgrade") {
    await runUpgrade(process.argv.slice(3), {
      version: VERSION,
      scriptPath: process.argv[1]
    });
    return;
  }

  if (process.argv[2] === "setup") {
    const target = process.argv[3];
    if (target !== "claude" && target !== "opencode") {
      throw new Error("Usage: roslyngate-ts setup claude|opencode [--solution <path>] [--skill] [--dry-run]");
    }
    runSetup(target, process.argv.slice(4));
    return;
  }

  const args = parseArgs(process.argv.slice(2));
  let workspace: TsWorkspace;

  try {
    const project = detectTypeScriptProject(args.solution, process.cwd(), args.projectKind);
    workspace = new TsWorkspace(project.root, project.configPath);
    workspace.load();
  } catch (cause) {
    const message = cause instanceof Error ? cause.message : String(cause);
    const hint = cause instanceof ProjectFailure ? cause.hint : undefined;
    if (args.check) {
      process.stdout.write(`roslyngate-ts: ${message}\n`);
      if (hint) process.stdout.write(`${hint}\n`);
      process.stdout.write("status       error\n");
      process.exit(1);
    }
    serverFailure(hint ? `${message}\n${hint}` : message);
    return;
  }

  if (args.check) {
    process.stdout.write([
      `roslyngate-ts ${VERSION}`,
      `config       ${workspace.configPath}`,
      `root         ${workspace.root}`,
      `files        ${workspace.sourceFileCount()}`,
      `projects     ${workspace.projectCount() + 1}`,
      `typescript   ${workspace.typescriptVersion()}`,
      `watch        ${process.env.ROSLYGATE_TS_WATCH === "off" ? "off" : "on"}`,
      "status       ok",
      ""
    ].join("\n"));
    workspace.close();
    return;
  }

  const server = new Server(
    { name: "roslyn-gate-ts", version: VERSION },
    { capabilities: { tools: {} }, instructions: INSTRUCTIONS }
  );

  server.setRequestHandler(ListToolsRequestSchema, () => ({
    tools: TOOL_DEFINITIONS
  }));

  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    try {
      return callTool(workspace, request.params.name, (request.params.arguments ?? {}) as Arguments, args);
    } catch (cause) {
      if (cause instanceof ToolFailure) {
        return error(cause.code, cause.message, cause.hint);
      }
      return error(
        "workspace_error",
        cause instanceof Error ? cause.message : String(cause),
        "Run roslyngate-ts --check to inspect the project."
      );
    }
  });

  await server.connect(new StdioServerTransport());
  process.on("SIGINT", () => process.exit(0));
  process.on("SIGTERM", () => process.exit(0));
}

function callTool(
  workspace: TsWorkspace,
  name: string,
  args: Arguments,
  options: Options
): ToolResult {
  switch (name) {
    case "get_symbol_definition":
      return definition(workspace, args, options);
    case "find_symbol_references":
      return references(workspace, args, options);
    case "get_type_hierarchy":
      return hierarchy(workspace, args, options);
    case "search_symbols":
      return search(workspace, args, options);
    case "reload_workspace":
      return reload(workspace);
    default:
      throw new ToolFailure("invalid_argument", `Unknown tool: ${name}.`, "Call tools/list to inspect the available tools.");
  }
}

function definition(workspace: TsWorkspace, args: Arguments, options: Options): ToolResult {
  const detail = readDetail(args.detail) ?? options.detail;
  const target = readTarget(args);
  const matches = resolveMatches(workspace, target, args.parameterTypes);
  const searchQuery = simpleName(target.name);

  if (matches.length === 0) {
    const body: Record<string, unknown> = detail === "Verbose"
      ? { symbol: null, ambiguous: false }
      : { symbol: null };
    addSuggestion(body, searchQuery);
    return text(withState(body, workspace));
  }

  if (matches.length > 1) {
    return text(withState(ambiguous(matches, detail), workspace));
  }

  const symbol = matches[0];
  const body = detail === "Verbose"
    ? { symbol: workspace.verbose(symbol), ambiguous: false }
    : workspace.compact(symbol);
  return text(withState(body, workspace));
}

function references(workspace: TsWorkspace, args: Arguments, options: Options): ToolResult {
  const detail = readDetail(args.detail) ?? options.detail;
  const target = readTarget(args);
  const matches = resolveMatches(workspace, target, args.parameterTypes);
  const offset = clampOffset(args.offset);
  const limit = clampLimit(args.limit, options.refLimit, 200);
  const includeDefinition = readBoolean(args.includeDefinition, false);
  const includeDeclarations = readBoolean(args.includeDeclarations, false);
  const searchQuery = simpleName(target.name);

  if (matches.length === 0) {
    const body: Record<string, unknown> = detail === "Verbose"
      ? {
          symbol: null,
          ambiguous: false,
          total: 0,
          offset,
          limit,
          truncated: false,
          references: []
        }
      : { symbol: null };
    addSuggestion(body, searchQuery);
    return text(withState(body, workspace));
  }

  if (matches.length > 1) {
    return text(withState(ambiguous(matches, detail), workspace));
  }

  const symbol = matches[0];
  const page = workspace.references(symbol, includeDeclarations, offset, limit);

  if (detail === "Verbose") {
    const body: Record<string, unknown> = {
      symbol: workspace.verbose(symbol),
      ambiguous: false,
      total: page.total,
      offset,
      limit: page.limit,
      truncated: page.truncated,
      references: page.refs.map((reference) => ({
        kind: reference.kind,
        file: reference.file,
        line: reference.line,
        column: reference.column
      }))
    };
    addNextPage(body, page.truncated, offset + page.refs.length, "find_symbol_references");
    return text(withState(body, workspace));
  }

  const body: Record<string, unknown> = {
    q: symbol.qualifiedName
  };
  if (includeDefinition) body.definition = workspace.compact(symbol);
  body.total = page.total;
  body.offset = offset;
  body.truncated = page.truncated;
  body.refs = page.refs.map((reference) => [reference.file, reference.line, reference.kind]);
  addNextPage(body, page.truncated, offset + page.refs.length, "find_symbol_references");
  return text(withState(body, workspace));
}

function hierarchy(workspace: TsWorkspace, args: Arguments, options: Options): ToolResult {
  const detail = readDetail(args.detail) ?? options.detail;
  const target = readTarget(args);
  const matches = resolveMatches(workspace, target);
  const searchQuery = simpleName(target.name);

  if (matches.length === 0) {
    const body: Record<string, unknown> = detail === "Verbose"
      ? { type: null, ambiguous: false }
      : { type: null };
    addSuggestion(body, searchQuery);
    return text(withState(body, workspace));
  }

  if (matches.length > 1) {
    return text(withState(ambiguous(matches, detail), workspace));
  }

  const symbol = matches[0];
  const result = workspace.hierarchy(symbol);

  if (detail === "Verbose") {
    return text(withState({
      type: workspace.verbose(symbol),
      ambiguous: false,
      baseTypes: result.base,
      interfaces: result.interfaces,
      derivedTypes: result.derived,
      implementations: result.implementations,
      truncated: result.truncated
    }, workspace));
  }

  const body: Record<string, unknown> = { t: symbol.qualifiedName };
  if (result.base.length > 0) body.b = result.base;
  if (result.interfaces.length > 0) body.i = result.interfaces;
  if (result.derived.length > 0) body.d = result.derived;
  if (result.implementations.length > 0) body.m = result.implementations;
  if (result.truncated) body.truncated = true;
  return text(withState(body, workspace));
}

function search(workspace: TsWorkspace, args: Arguments, options: Options): ToolResult {
  const detail = readDetail(args.detail) ?? options.detail;
  const query = readString(args.query);
  if (!query || query.trim().length < 2) {
    throw new ToolFailure(
      "query_too_short",
      "Search query must contain at least 2 characters.",
      "Use a fragment such as Widget, Open, or Door."
    );
  }

  const offset = clampOffset(args.offset);
  const limit = clampLimit(args.limit, options.searchLimit, 50);
  const page = workspace.search(
    query,
    readString(args.kind),
    readString(args.namespace),
    offset,
    limit
  );

  if (detail === "Verbose") {
    const body: Record<string, unknown> = {
      total: page.total,
      offset,
      limit: page.limit,
      truncated: page.truncated,
      hits: page.hits.map((hit) => workspace.verbose(hit))
    };
    addNextPage(body, page.truncated, offset + page.hits.length, "search_symbols");
    return text(withState(body, workspace));
  }

  const body: Record<string, unknown> = {
    total: page.total,
    offset,
    truncated: page.truncated,
    hits: page.hits.map((hit) => [
      hit.qualifiedName,
      hit.kind,
      hit.location.file,
      hit.location.line
    ])
  };
  addNextPage(body, page.truncated, offset + page.hits.length, "search_symbols");
  return text(withState(body, workspace));
}

function reload(workspace: TsWorkspace): ToolResult {
  workspace.reload();
  return text({
    status: "reloaded",
    projects: workspace.projectCount() + 1,
    files: workspace.sourceFileCount(),
    sdk: `typescript ${workspace.typescriptVersion()}`,
    watch: process.env.ROSLYGATE_TS_WATCH === "off" ? "off" : "on"
  });
}

function resolveMatches(
  workspace: TsWorkspace,
  target: Target,
  parameterTypes?: unknown
): SymbolInfo[] {
  if (target.file && !workspace.isFileIndexed(target.file)) {
    throw new ToolFailure(
      "file_not_indexed",
      `The file is not part of the loaded workspace: ${target.file}.`,
      "Call reload_workspace, then retry."
    );
  }

  const types = readStringArray(parameterTypes);
  const matches = workspace.resolve(target.name, target.file, target.line, target.column);
  return matches.filter((match) => matchesParameterTypes(match, types));
}

function ambiguous(matches: SymbolInfo[], detail: Detail): Record<string, unknown> {
  if (detail === "Verbose") {
    return {
      symbol: null,
      ambiguous: true,
      candidates: matches.slice(0, 20).map((match) => verboseSymbol(match)),
      hint: AMBIGUOUS_HINT
    };
  }
  return {
    ambiguous: true,
    candidates: matches.slice(0, 20).map((match) => compactCandidate(match)),
    hint: AMBIGUOUS_HINT
  };
}

function verboseSymbol(symbol: SymbolInfo): Record<string, unknown> {
  const value: Record<string, unknown> = {
    name: symbol.name,
    qualifiedName: symbol.qualifiedName,
    kind: symbol.kind
  };
  if (symbol.namespace) value.namespace = symbol.namespace;
  if (symbol.containingType) value.containingType = symbol.containingType;
  value.signature = symbol.signature;
  if (symbol.documentation) value.documentation = symbol.documentation;
  value.location = symbol.location;
  return value;
}

function compactCandidate(symbol: SymbolInfo): [string, string, string, string, number] {
  return [
    symbol.qualifiedName,
    symbol.kind,
    symbol.signature,
    symbol.location.file,
    symbol.location.line
  ];
}

function addSuggestion(body: Record<string, unknown>, query: string | undefined): void {
  if (!query || query.length < 2) return;
  body.hint = NOT_FOUND_HINT;
  body.try = { tool: "search_symbols", arguments: { query } };
}

function addNextPage(
  body: Record<string, unknown>,
  truncated: boolean,
  nextOffset: number,
  tool: string
): void {
  if (!truncated) return;
  body.nextOffset = nextOffset;
  body.hint = `Call ${tool} again with offset=${nextOffset} and the same arguments.`;
}

function withState(body: Record<string, unknown>, workspace: TsWorkspace): Record<string, unknown> {
  const state = workspace.state();
  if (!state.stale) return body;
  return {
    ...body,
    stale: true,
    changed: state.changed,
    hint: STALE_HINT,
    try: { tool: "reload_workspace", arguments: {} }
  };
}

function text(value: unknown): ToolResult {
  return { content: [{ type: "text", text: JSON.stringify(value) }] };
}

function error(code: string, message: string, hint: string): ToolResult {
  return {
    isError: true,
    content: [{ type: "text", text: JSON.stringify({ error: { code, message, hint } }) }]
  };
}

type Detail = "Compact" | "Verbose";

type Target = {
  name?: string;
  file?: string;
  line?: number;
  column?: number;
};

type Options = {
  solution?: string;
  projectKind?: "ts" | "dotnet";
  check: boolean;
  detail: Detail;
  refLimit: number;
  searchLimit: number;
};

function readTarget(args: Arguments): Target {
  const target: Target = {
    name: readString(args.name),
    file: readString(args.file),
    line: readInteger(args.line, "line"),
    column: readInteger(args.column, "column")
  };
  if (!target.name && !(target.file && target.line)) {
    throw new ToolFailure(
      "invalid_target",
      "No symbol name or position was provided.",
      "Pass name, or file with a 1-based line."
    );
  }
  if (target.file && !target.line) {
    throw new ToolFailure(
      "invalid_target",
      "A file target requires a 1-based line.",
      "Pass file and line, or a symbol name."
    );
  }
  return target;
}

function readString(value: unknown): string | undefined {
  if (value === undefined || value === null) return undefined;
  if (typeof value !== "string") {
    throw new ToolFailure("invalid_argument", "Expected a string argument.", "Check the tool input schema.");
  }
  return value;
}

function readInteger(value: unknown, name: string): number | undefined {
  if (value === undefined || value === null) return undefined;
  if (typeof value !== "number" || !Number.isInteger(value)) {
    throw new ToolFailure("invalid_argument", `${name} must be an integer.`, "Check the tool input schema.");
  }
  return value;
}

function readBoolean(value: unknown, fallback: boolean): boolean {
  if (value === undefined || value === null) return fallback;
  if (typeof value !== "boolean") {
    throw new ToolFailure("invalid_argument", "Expected a boolean argument.", "Check the tool input schema.");
  }
  return value;
}

function readStringArray(value: unknown): string[] | undefined {
  if (value === undefined || value === null) return undefined;
  if (!Array.isArray(value) || value.some((item) => typeof item !== "string")) {
    throw new ToolFailure("invalid_argument", "Expected an array of strings.", "Check the tool input schema.");
  }
  return value as string[];
}

function readDetail(value: unknown): Detail | undefined {
  if (value === undefined || value === null) return undefined;
  if (value === "Compact" || value === "Verbose") return value;
  throw new ToolFailure("invalid_argument", "detail must be Compact or Verbose.", "Use detail=Compact or detail=Verbose.");
}

function clampOffset(value: unknown): number {
  const number = readInteger(value, "offset") ?? 0;
  return Math.max(0, number);
}

function clampLimit(value: unknown, fallback: number, maximum: number): number {
  const number = readInteger(value, "limit") ?? fallback;
  return Math.max(1, Math.min(number, maximum));
}

function simpleName(name: string | undefined): string | undefined {
  if (!name) return undefined;
  const trimmed = name.trim().split("(")[0];
  const simple = trimmed.slice(trimmed.lastIndexOf(".") + 1);
  return simple.length >= 2 ? simple : undefined;
}

function parseArgs(argv: string[]): Options {
  let solution: string | undefined;
  let projectKind: "ts" | "dotnet" | undefined;
  let check = false;
  let detail: Detail = "Compact";
  let refLimit = 50;
  let searchLimit = 20;

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--solution" || arg === "--project") {
      solution = argv[++index] ?? solution;
    } else if (arg === "--dotnet") {
      projectKind = "dotnet";
    } else if (arg === "--ts") {
      projectKind = "ts";
    } else if (arg === "--check") {
      check = true;
    } else if (arg === "--no-watch") {
      process.env.ROSLYGATE_TS_WATCH = "off";
    } else if (arg === "--watch") {
      delete process.env.ROSLYGATE_TS_WATCH;
    } else if (arg === "--detail") {
      detail = parseDetail(argv[++index]);
    } else if (arg === "--ref-limit") {
      refLimit = parseBounded(argv[++index], 1, 200, "--ref-limit");
    } else if (arg === "--search-limit") {
      searchLimit = parseBounded(argv[++index], 1, 50, "--search-limit");
    } else if (arg === "--version" || arg === "-v") {
      process.stdout.write(`${VERSION}\n`);
      process.exit(0);
    } else if (arg === "--help" || arg === "-h") {
      process.stdout.write("Usage: roslyngate-ts [--solution <dir|tsconfig>] [--dotnet|--ts] [--check] [--no-watch] [--detail compact|verbose] [--ref-limit <n>] [--search-limit <n>]\n");
      process.exit(0);
    }
  }

  return { solution, projectKind, check, detail, refLimit, searchLimit };
}

function parseDetail(value: string | undefined): Detail {
  if (value === "compact" || value === "Compact") return "Compact";
  if (value === "verbose" || value === "Verbose") return "Verbose";
  throw new Error("--detail must be compact or verbose.");
}

function parseBounded(value: string | undefined, minimum: number, maximum: number, option: string): number {
  const number = Number(value);
  if (!Number.isInteger(number) || number < minimum || number > maximum) {
    throw new Error(`${option} must be an integer between ${minimum} and ${maximum}.`);
  }
  return number;
}

class ToolFailure extends Error {
  constructor(
    readonly code: string,
    message: string,
    readonly hint: string
  ) {
    super(message);
  }
}

function serverFailure(message: string): void {
  process.stderr.write(`roslyngate-ts: ${message}\n`);
  process.exit(1);
}

main().catch((cause) => serverFailure(cause instanceof Error ? cause.message : String(cause)));
