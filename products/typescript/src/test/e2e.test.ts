import assert from "node:assert/strict";
import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import readline from "node:readline";
import test from "node:test";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const cli = path.resolve(here, "..", "cli.js");
const fixture = path.resolve(here, "../../../../fixtures/TsSample");

class Session {
  private readonly process: ChildProcessWithoutNullStreams;
  private readonly pending = new Map<number, (value: Record<string, unknown>) => void>();
  private nextId = 0;

  constructor(readonly solution: string, extra: string[] = []) {
    this.process = spawn(process.execPath, [cli, "--solution", solution, ...extra], {
      stdio: ["pipe", "pipe", "pipe"]
    });
    const lines = readline.createInterface({ input: this.process.stdout });
    lines.on("line", (line) => {
      const message = JSON.parse(line) as Record<string, unknown>;
      const id = message.id;
      if (typeof id !== "number") return;
      this.pending.get(id)?.(message);
      this.pending.delete(id);
    });
  }

  request(method: string, params?: Record<string, unknown>): Promise<Record<string, unknown>> {
    this.nextId += 1;
    const id = this.nextId;
    const message = { jsonrpc: "2.0", id, method, ...(params ? { params } : {}) };
    return new Promise((resolve) => {
      this.pending.set(id, resolve);
      this.process.stdin.write(`${JSON.stringify(message)}\n`);
    });
  }

  async initialize(): Promise<void> {
    await this.request("initialize", {
      protocolVersion: "2025-06-18",
      capabilities: {},
      clientInfo: { name: "ts-e2e", version: "0" }
    });
    this.process.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" })}\n`);
  }

  async callTool(name: string, args: Record<string, unknown>): Promise<any> {
    const response = await this.request("tools/call", { name, arguments: args });
    const result = response.result as Record<string, any>;
    assert.equal(result.isError, undefined, JSON.stringify(response));
    return JSON.parse(result.content[0].text);
  }

  async callError(name: string, args: Record<string, unknown>): Promise<any> {
    const response = await this.request("tools/call", { name, arguments: args });
    const result = response.result as Record<string, any>;
    assert.equal(result.isError, true, JSON.stringify(response));
    return JSON.parse(result.content[0].text);
  }

  close(): void {
    this.process.stdin.end();
    this.process.kill();
  }
}

async function withSession<T>(
  solution: string,
  body: (session: Session) => Promise<T>,
  extra: string[] = ["--no-watch"]
): Promise<T> {
  const session = new Session(solution, extra);
  try {
    await session.initialize();
    return await body(session);
  } finally {
    session.close();
  }
}

test("exposes the shared five-tool contract", async () => {
  await withSession(fixture, async (session) => {
    const response = await session.request("tools/list");
    const tools = (response.result as any).tools as Array<{ name: string }>;
    assert.deepEqual(tools.map((tool) => tool.name).sort(), [
      "find_symbol_references",
      "get_symbol_definition",
      "get_type_hierarchy",
      "reload_workspace",
      "search_symbols"
    ]);
  });
});

test("resolves definitions, overloads, ambiguity, and verbose output", async () => {
  await withSession(fixture, async (session) => {
    const definition = await session.callTool("get_symbol_definition", { name: "Widget" });
    assert.equal(definition.n, "Widget");
    assert.equal(definition.k, "class");
    assert.equal(definition.f, "src/widget.ts");

    const ambiguous = await session.callTool("get_symbol_definition", { name: "open" });
    assert.equal(ambiguous.ambiguous, true);
    assert.ok(ambiguous.candidates.length > 1);

    const overload = await session.callTool("get_symbol_definition", {
      name: "open",
      parameterTypes: ["number"]
    });
    assert.equal(overload.n, "open");
    assert.equal(overload.k, "method");

    const verbose = await session.callTool("get_symbol_definition", {
      name: "Widget",
      detail: "Verbose"
    });
    assert.equal(verbose.ambiguous, false);
    assert.equal(verbose.symbol.qualifiedName, "src/widget.Widget");
    assert.equal(verbose.symbol.location.column, 1);

    const missing = await session.callTool("get_symbol_definition", { name: "NoSuchSymbol" });
    assert.equal(missing.symbol, null);
    assert.equal(missing.try.tool, "search_symbols");
  });
});

test("pages references and honors declaration and definition flags", async () => {
  await withSession(fixture, async (session) => {
    const page = await session.callTool("find_symbol_references", {
      name: "Widget",
      limit: 1
    });
    assert.equal(page.total, 2);
    assert.equal(page.truncated, true);
    assert.equal(page.nextOffset, 1);
    assert.match(page.hint, /offset=1/);
    assert.deepEqual(page.refs[0].slice(0, 3), ["src/door.ts", 3, "read"]);

    const withDefinition = await session.callTool("find_symbol_references", {
      name: "Widget",
      includeDefinition: true
    });
    assert.equal(withDefinition.definition.n, "Widget");
    assert.equal(withDefinition.definition.q, "src/widget.Widget");

    const withDeclarations = await session.callTool("find_symbol_references", {
      name: "Widget",
      includeDeclarations: true
    });
    assert.ok(withDeclarations.total > withDefinition.total);

    const verbose = await session.callTool("find_symbol_references", {
      name: "Widget",
      detail: "Verbose"
    });
    assert.equal(verbose.ambiguous, false);
    assert.equal(verbose.references[0].kind, "read");
    assert.equal(verbose.references[1].kind, "inherit");
  });
});

test("returns hierarchy and search pages", async () => {
  await withSession(fixture, async (session) => {
    const hierarchy = await session.callTool("get_type_hierarchy", { name: "Widget" });
    assert.equal(hierarchy.t, "src/widget.Widget");
    assert.deepEqual(hierarchy.i, ["src/widget.Openable"]);
    assert.deepEqual(hierarchy.d, ["src/widget.SecureWidget"]);

    const implemented = await session.callTool("get_type_hierarchy", { name: "Openable" });
    assert.deepEqual(implemented.m, ["src/widget.Widget"]);

    const verbose = await session.callTool("get_type_hierarchy", {
      name: "Widget",
      detail: "Verbose"
    });
    assert.equal(verbose.type.name, "Widget");
    assert.equal(verbose.ambiguous, false);

    const search = await session.callTool("search_symbols", { query: "Widget", limit: 1 });
    assert.equal(search.truncated, true);
    assert.equal(search.nextOffset, 1);
    assert.equal(search.hits[0].length, 4);

    const filtered = await session.callTool("search_symbols", {
      query: "Widget",
      kind: "class"
    });
    assert.ok(filtered.hits.every((hit: unknown[]) => hit[1] === "class"));

    const verboseSearch = await session.callTool("search_symbols", {
      query: "Widget",
      limit: 1,
      detail: "Verbose"
    });
    assert.equal(verboseSearch.hits[0].qualifiedName, "src/door.useWidget");
  });
});

test("returns teaching errors and reloads the workspace", async () => {
  await withSession(fixture, async (session) => {
    const short = await session.callError("search_symbols", { query: "W" });
    assert.equal(short.error.code, "query_too_short");

    const target = await session.callError("get_symbol_definition", {});
    assert.equal(target.error.code, "invalid_target");

    const file = await session.callError("get_symbol_definition", {
      file: "missing.ts",
      line: 1
    });
    assert.equal(file.error.code, "file_not_indexed");

    const reload = await session.callTool("reload_workspace", {});
    assert.equal(reload.status, "reloaded");
    assert.equal(reload.files, 4);
    assert.equal(reload.watch, "off");
  });
});

test("marks stale after a watched edit and clears it after reload", async () => {
  const temporary = fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-ts-"));
  fs.cpSync(fixture, temporary, { recursive: true });

  await withSession(temporary, async (session) => {
    const before = await session.callTool("get_symbol_definition", { name: "Widget" });
    assert.equal(before.stale, undefined);

    fs.appendFileSync(path.join(temporary, "src", "widget.ts"), "\n");
    let stale: any;
    for (let attempt = 0; attempt < 40; attempt += 1) {
      stale = await session.callTool("get_symbol_definition", { name: "Widget" });
      if (stale.stale === true) break;
      await new Promise((resolve) => setTimeout(resolve, 50));
    }
    assert.equal(stale.stale, true);
    assert.equal(stale.try.tool, "reload_workspace");

    const reload = await session.callTool("reload_workspace", {});
    assert.equal(reload.status, "reloaded");

    const after = await session.callTool("get_symbol_definition", { name: "Widget" });
    assert.equal(after.stale, undefined);
  }, []);
  fs.rmSync(temporary, { recursive: true, force: true });
});
