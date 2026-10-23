const fs = require("node:fs");
const fsp = fs.promises;
const path = require("node:path");
const { assetName, resolveRid } = require("./platform");
const { binaryPath } = require("./cache");
const { ensureBinary } = require("./download");
const { writeSkill } = require("./skill");
const { parseArgs } = require("./setup-args");

const CONFIG_FILE = ".mcp.json";
const SERVER_NAME = "roslyngate";

function resolveSolution(value, cwd = process.cwd()) {
  const absolute = path.resolve(cwd, value);
  let stats;

  try {
    stats = fs.statSync(absolute);
  } catch {
    throw new Error(`Solution path does not exist: ${absolute}`);
  }

  if (stats.isDirectory()) {
    return { solution: absolute, root: absolute };
  }

  if (!/\.(sln|slnx)$/i.test(absolute)) {
    throw new Error(`Expected a .sln or .slnx file, or a directory: ${absolute}`);
  }

  return { solution: absolute, root: path.dirname(absolute) };
}

function buildEntry(binary, solution) {
  return {
    command: binary,
    args: ["--solution", solution]
  };
}

function mergeConfig(existing, entry) {
  const config = existing && typeof existing === "object" && !Array.isArray(existing)
    ? existing
    : {};

  if (config.mcpServers === undefined) {
    config.mcpServers = {};
  } else if (
    config.mcpServers === null
    || typeof config.mcpServers !== "object"
    || Array.isArray(config.mcpServers)
  ) {
    throw new Error(`Existing ${CONFIG_FILE} has an invalid mcpServers value.`);
  }

  config.mcpServers[SERVER_NAME] = entry;
  return config;
}

function writeConfig(root, entry) {
  const file = path.join(root, CONFIG_FILE);
  let existing = {};

  if (fs.existsSync(file)) {
    const raw = fs.readFileSync(file, "utf8");
    try {
      existing = JSON.parse(raw);
    } catch {
      throw new Error(`Existing ${file} is not valid JSON. Fix it or remove it.`);
    }
  }

  const config = mergeConfig(existing, entry);
  const json = `${JSON.stringify(config, null, 2)}\n`;
  const temporary = `${file}.${process.pid}.tmp`;

  fs.writeFileSync(temporary, json);
  fs.renameSync(temporary, file);
  return { file, json };
}

async function runSetup(args, options = {}) {
  const { version, cwd = process.cwd(), env = process.env } = options;
  const { solution: solutionArg, print, dryRun, skill } = parseArgs(args);
  const { solution, root } = resolveSolution(solutionArg, cwd);

  let binary = env.ROSLYGATE_BINARY
    ? path.resolve(cwd, env.ROSLYGATE_BINARY)
    : null;

  if (binary && !fs.existsSync(binary)) {
    throw new Error(`ROSLYGATE_BINARY does not exist: ${binary}`);
  }

  if (!binary) {
    const rid = resolveRid(process.platform, process.arch);
    const asset = assetName(rid);
    binary = binaryPath({ version, asset, env, platform: process.platform });

    if (!print && !dryRun) {
      binary = await ensureBinary({ asset, version, target: binary });
    }
  }

  const entry = buildEntry(binary, solution);

  if (print) {
    process.stdout.write(`${JSON.stringify(mergeConfig({}, entry), null, 2)}\n`);
    return;
  }

  const file = path.join(root, CONFIG_FILE);
  if (dryRun) {
    process.stdout.write(`Would write ${file}:\n`);
    process.stdout.write(`${JSON.stringify(mergeConfig({}, entry), null, 2)}\n`);

    if (skill) {
      const result = writeSkill(root, { dryRun: true });
      process.stdout.write(
        result.written
          ? `Would write ${result.file}\n`
          : `Would leave existing ${result.file}\n`
      );
    }

    return;
  }

  const written = writeConfig(root, entry);
  process.stdout.write(`roslyngate: wrote ${written.file}\n`);

  if (skill) {
    const result = writeSkill(root);
    process.stdout.write(
      result.written
        ? `roslyngate: wrote ${result.file}\n`
        : `roslyngate: left existing ${result.file}\n`
    );
  }

  process.stdout.write("Next: open Claude Code in that repository, run /mcp, and approve roslyngate.\n");
}

module.exports = {
  CONFIG_FILE,
  SERVER_NAME,
  buildEntry,
  mergeConfig,
  parseArgs,
  resolveSolution,
  runSetup,
  writeConfig
};
