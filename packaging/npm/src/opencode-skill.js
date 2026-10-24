const fs = require("node:fs");
const path = require("node:path");

const SKILL_NAME = "roslyn-symbols";
const SKILL_FILE = path.join(".opencode", "skills", SKILL_NAME, "SKILL.md");

const SkillText = `---
name: RoslynGate Symbols
description: Resolve .NET symbols with RoslynGate before grep or file reads. Use for definitions, references, overloads, implementations, and cross-project navigation with minimal context.
---

# RoslynGate symbol workflow

Use RoslynGate MCP tools for every .NET symbol question. Do not fall back to
grep, glob, read, or subagents while a RoslynGate result is available.

1. For an exact qualified name, call \`find_symbol_references\` once with
   \`includeDefinition=true\` and \`limit=200\`. Do not call
   \`get_symbol_definition\` separately.
2. If the name is ambiguous, pass \`parameterTypes\` (for example
   \`["string"]\`). Use file plus line only if types do not resolve it.
3. If \`truncated\` is true, call the same tool with \`offset=nextOffset\` only
   when the user asks for all references; otherwise report the first page.
4. If \`stale\` is true, call \`reload_workspace\` once and retry. Never treat
   an empty stale result as confirmed.
5. Return compact \`file:line\` entries. Do not paste source or explain the
   architecture unless the user asks.
6. For type relationships use \`get_type_hierarchy\`; for unknown names use
   \`search_symbols\` once, then resolve the chosen candidate.
`;

function skillPath(root, { global = false } = {}) {
  return global
    ? path.join(root, "skills", SKILL_NAME, "SKILL.md")
    : path.join(root, SKILL_FILE);
}

function writeSkill(root, { dryRun = false, global = false } = {}) {
  const file = skillPath(root, { global });
  if (fs.existsSync(file)) {
    return { written: false, reason: "exists", file };
  }

  if (!dryRun) {
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, SkillText);
  }

  return { written: true, reason: "created", file };
}

module.exports = {
  SKILL_FILE,
  SKILL_NAME,
  SkillText,
  skillPath,
  writeSkill
};
