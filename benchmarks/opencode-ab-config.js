const fs = require("node:fs");
const path = require("node:path");

const SERVER_NAME = "roslyngate";
const SKILL_ID = "roslyn-symbols";

function withConfig(binary, solution) {
  return {
    $schema: "https://opencode.ai/config.json",
    mcp: {
      servers: {
        [SERVER_NAME]: {
          type: "local",
          command: [binary, "--solution", solution],
          codemode: false,
          timeout: { startup: 60000, catalog: 60000 }
        }
      }
    },
    permissions: [
      ...deniedBuiltins(),
      { action: "skill", resource: SKILL_ID, effect: "allow" }
    ],
    instructions: [`.opencode/skills/${SKILL_ID}/SKILL.md`]
  };
}

function withoutConfig() {
  return {
    $schema: "https://opencode.ai/config.json",
    codemode: false,
    permissions: [
      ...deniedBuiltins(),
      { action: "read", resource: "*", effect: "allow" },
      { action: "glob", resource: "*", effect: "allow" },
      { action: "grep", resource: "*", effect: "allow" }
    ]
  };
}

function deniedBuiltins() {
  return ["read", "glob", "grep", "shell", "edit", "webfetch", "websearch", "subagent"].map(
    (action) => ({ action, resource: "*", effect: "deny" })
  );
}

function configFile(solution) {
  return path.join(solution, "opencode.jsonc");
}

function backupConfig(solution, { hideSkill = false } = {}) {
  const files = ["opencode.jsonc", "opencode.json"].map((name) => path.join(solution, name));
  const backups = files
    .filter((file) => fs.existsSync(file))
    .map((file) => ({ file, backup: `${file}.opencode-ab.bak` }));

  for (const item of backups) {
    fs.renameSync(item.file, item.backup);
  }

  const skill = path.join(solution, ".opencode", "skills", SKILL_ID);
  const skillBackup = `${skill}.opencode-ab.bak`;
  const skillBackedUp = hideSkill && fs.existsSync(skill);
  if (skillBackedUp) {
    fs.renameSync(skill, skillBackup);
  }

  return () => {
    if (fs.existsSync(configFile(solution))) {
      fs.rmSync(configFile(solution), { force: true });
    }
    for (const item of backups) {
      if (fs.existsSync(item.backup)) {
        fs.renameSync(item.backup, item.file);
      }
    }
    if (skillBackedUp && fs.existsSync(skillBackup)) {
      fs.renameSync(skillBackup, skill);
    }
  };
}

function writeConfig(solution, config) {
  fs.writeFileSync(configFile(solution), `${JSON.stringify(config, null, 2)}\n`);
}

module.exports = {
  SERVER_NAME,
  SKILL_ID,
  backupConfig,
  configFile,
  deniedBuiltins,
  withConfig,
  withoutConfig,
  writeConfig
};
