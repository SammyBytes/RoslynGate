import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { detectTypeScriptProject } from "./project.js";

const SERVER = "roslyngate-ts";
const SKILL = `---
name: RoslynGate TS Symbols
description: Resolve TypeScript/JavaScript symbols with RoslynGate TS before grep or file reads. Use for definitions, references, overloads, hierarchy, and cross-project navigation with minimal context.
---

# RoslynGate TS workflow

Use the RoslynGate TS MCP tools for .NET-style symbol questions in TypeScript,
JavaScript, TSX, and JSX.

1. For an exact qualified name, call \`find_symbol_references\` once with
   \`includeDefinition=true\` and \`limit=200\`.
2. If a name is ambiguous, pass \`parameterTypes\`; use file plus line only as a
   fallback.
3. If \`truncated\` is true, use \`offset=nextOffset\` only when all references
   are requested.
4. If \`stale\` is true, call \`reload_workspace\` once and retry.
5. Use \`get_type_hierarchy\` for type relationships and \`search_symbols\` only
   for unknown names.
6. Answer with compact \`file:line\` entries. Do not paste source or use
   subagents for symbol lookup.
`;

export function parseSetup(args: string[]) {
  let solution = ".";
  let skill = false;
  let dryRun = false;
  for (let i = 0; i < args.length; i += 1) {
    const arg = args[i];
    if (arg === "--solution") solution = args[++i] ?? solution;
    else if (arg === "--skill") skill = true;
    else if (arg === "--dry-run") dryRun = true;
    else throw new Error(`Unknown setup option: ${arg}`);
  }
  return { solution: path.resolve(solution), skill, dryRun };
}

export function runSetup(target: "claude" | "opencode", args: string[]) {
  const { solution, skill, dryRun } = parseSetup(args);
  const root = resolveRoot(solution);
  detectTypeScriptProject(root);
  const cli = fileURLToPath(new URL("./cli.js", import.meta.url));
  const entry = target === "claude"
    ? { command: process.execPath, args: [cli, "--solution", root] }
    : {
        type: "local",
        command: [process.execPath, cli, "--solution", root],
        codemode: false,
        timeout: { startup: 60000, catalog: 60000 }
      };

  const file = target === "claude"
    ? path.join(root, ".mcp.json")
    : path.join(root, "opencode.jsonc");
  const config = readJson(file);
  if (target === "claude") {
    config.mcpServers ??= {};
    config.mcpServers[SERVER] = entry;
  } else {
    config.$schema = "https://opencode.ai/config.json";
    config.mcp ??= {};
    config.mcp.servers ??= {};
    config.mcp.servers[SERVER] = entry;
    config.permissions = [
      ...deniedBuiltins(),
      { action: "skill", resource: "roslyn-symbols-ts", effect: "allow" }
    ];
    if (skill) {
      config.instructions ??= [];
      const instruction = ".opencode/skills/roslyn-symbols-ts/SKILL.md";
      if (!config.instructions.includes(instruction)) config.instructions.push(instruction);
    }
  }

  const json = `${JSON.stringify(config, null, 2)}\n`;
  if (dryRun) {
    process.stdout.write(`Would write ${file}:\n${json}`);
    return;
  }
  fs.writeFileSync(file, json);
  process.stdout.write(`roslyngate-ts: wrote ${file}\n`);

  if (skill) {
    const skillFile = target === "claude"
      ? path.join(root, ".claude/skills/roslyn-symbols-ts/SKILL.md")
      : path.join(root, ".opencode/skills/roslyn-symbols-ts/SKILL.md");
    fs.mkdirSync(path.dirname(skillFile), { recursive: true });
    fs.writeFileSync(skillFile, SKILL);
    process.stdout.write(`roslyngate-ts: wrote ${skillFile}\n`);
  }
}

function deniedBuiltins() {
  return ["read", "glob", "grep", "shell", "edit", "webfetch", "websearch", "subagent"].map(
    (action) => ({ action, resource: "*", effect: "deny" })
  );
}

function resolveRoot(solution: string): string {
  const absolute = path.resolve(solution);
  if (fs.statSync(absolute).isFile()) return path.dirname(absolute);
  return absolute;
}

function readJson(file: string): Record<string, any> {
  if (!fs.existsSync(file)) return {};
  try {
    return JSON.parse(fs.readFileSync(file, "utf8"));
  } catch {
    throw new Error(`${file} is not valid JSON. Fix it or remove it.`);
  }
}
