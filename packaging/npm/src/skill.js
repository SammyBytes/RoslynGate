const fs = require("node:fs");
const path = require("node:path");

const SKILL_NAME = "roslyn-symbols";
const SKILL_FILE = path.join(".claude", "skills", SKILL_NAME, "SKILL.md");

const SkillText = `---
name: ${SKILL_NAME}
description: Resolve .NET symbols with RoslynGate before using Grep or Read. Use for definitions, references, implementations, and cross-project C# navigation.
allowed-tools: mcp__roslyngate__get_symbol_definition mcp__roslyngate__find_symbol_references mcp__roslyngate__get_type_hierarchy mcp__roslyngate__search_symbols
---

When a task involves a .NET symbol:

1. For an exact qualified name, call \`find_symbol_references\` with
   \`includeDefinition=true\`; do not call \`get_symbol_definition\` separately.
2. If the name is ambiguous or you only know a fragment, call
   \`search_symbols\` and retry with the file and line.
3. If overloads are ambiguous, pass \`parameterTypes\` (for example
   \`["string"]\`) instead of choosing by file and line.
4. If \`truncated\` is true, call the same tool again with
   \`offset=nextOffset\` until it is false.
5. If \`stale\` is true, call \`reload_workspace\` and retry; do not treat an
   empty result as confirmed.
6. Prefer RoslynGate over Grep or Read for symbol locations. Read source only
   when the text itself matters.
7. Answer with \`file:line\` entries.
`;

function skillPath(root) {
  return path.join(root, SKILL_FILE);
}

function writeSkill(root, { dryRun = false } = {}) {
  const file = skillPath(root);
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
