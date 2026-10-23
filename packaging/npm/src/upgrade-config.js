const fs = require("node:fs");
const path = require("node:path");

const CONFIG_FILE = ".mcp.json";
const SERVER_NAME = "roslyngate";

function updateConfig(root, binary, { canUpdate } = {}) {
  const file = path.join(root, CONFIG_FILE);
  if (!fs.existsSync(file)) {
    return { updated: false, reason: "missing", file };
  }

  let config;
  try {
    config = JSON.parse(fs.readFileSync(file, "utf8"));
  } catch {
    throw new Error(`${file} is not valid JSON.`);
  }

  const entry = config?.mcpServers?.[SERVER_NAME];
  if (!entry || typeof entry !== "object" || Array.isArray(entry)) {
    return { updated: false, reason: "missing-entry", file };
  }

  if (canUpdate && !canUpdate(entry.command)) {
    return { updated: false, reason: "unmanaged", command: entry.command, file };
  }

  entry.command = binary;
  const temporary = `${file}.${process.pid}.tmp`;
  fs.writeFileSync(temporary, `${JSON.stringify(config, null, 2)}\n`);
  fs.renameSync(temporary, file);

  return { updated: true, file };
}

module.exports = {
  CONFIG_FILE,
  SERVER_NAME,
  updateConfig
};
