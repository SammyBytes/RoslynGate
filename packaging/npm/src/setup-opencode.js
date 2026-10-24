const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { assetName, resolveRid } = require("./platform");
const { binaryPath } = require("./cache");
const { ensureBinary } = require("./download");
const { resolveSolution } = require("./setup");
const { readConfig } = require("./jsonc");
const { writeSkill } = require("./opencode-skill");

const SERVER_NAME = "roslyngate";
const SCHEMA = "https://opencode.ai/config.json";

function parseArgs(args) {
  let solution = ".";
  let print = false;
  let dryRun = false;
  let global = false;
  let skill = false;

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];

    if (argument === "opencode") {
      continue;
    }

    if (argument === "--print") {
      print = true;
      continue;
    }

    if (argument === "--dry-run") {
      dryRun = true;
      continue;
    }

    if (argument === "--global") {
      global = true;
      continue;
    }

    if (argument === "--skill") {
      skill = true;
      continue;
    }

    if (argument === "--solution") {
      if (index + 1 >= args.length) {
        throw new Error("Missing value for --solution.");
      }
      solution = args[++index];
      continue;
    }

    if (argument.startsWith("--solution=")) {
      solution = argument.slice("--solution=".length);
      continue;
    }

    throw new Error(`Unknown setup option: ${argument}`);
  }

  return { solution, print, dryRun, global, skill };
}

function configRoot({ global, cwd, env }) {
  if (!global) {
    return cwd;
  }

  const base = env.XDG_CONFIG_HOME || path.join(os.homedir(), ".config");
  return path.join(base, "opencode");
}

function configFile(root) {
  const json = path.join(root, "opencode.json");
  return fs.existsSync(json) ? json : path.join(root, "opencode.jsonc");
}

function buildEntry(binary, solution) {
  return {
    type: "local",
    command: [binary, "--solution", solution],
    codemode: false,
    timeout: { startup: 60000, catalog: 60000 }
  };
}

function mergeConfig(config, entry) {
  const result = config && typeof config === "object" && !Array.isArray(config)
    ? config
    : {};

  result.$schema = SCHEMA;
  result.mcp = result.mcp && typeof result.mcp === "object" ? result.mcp : {};
  result.mcp.servers = result.mcp.servers && typeof result.mcp.servers === "object"
    ? result.mcp.servers
    : {};
  result.mcp.servers[SERVER_NAME] = entry;
  return result;
}

function writeConfig(file, config) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temporary = `${file}.${process.pid}.tmp`;
  fs.writeFileSync(temporary, `${JSON.stringify(config, null, 2)}\n`);
  fs.renameSync(temporary, file);
}

async function runSetup(args, options = {}) {
  const { version, cwd = process.cwd(), env = process.env } = options;
  const { solution: solutionArg, print, dryRun, global, skill } = parseArgs(args);
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

  const targetRoot = configRoot({ global, cwd: root, env });
  const file = configFile(targetRoot);
  const entry = buildEntry(binary, solution);
  const config = mergeConfig(readConfig(file), entry);

  if (skill && !global) {
    const instruction = ".opencode/skills/roslyn-symbols/SKILL.md";
    const current = config.instructions;
    const values = Array.isArray(current)
      ? current
      : current
        ? [current]
        : [];
    if (!values.includes(instruction)) {
      values.push(instruction);
    }
    config.instructions = values;
  }

  if (print) {
    process.stdout.write(`${JSON.stringify(config, null, 2)}\n`);
    return;
  }

  if (dryRun) {
    process.stdout.write(`Would write ${file}:\n`);
    process.stdout.write(`${JSON.stringify(config, null, 2)}\n`);

    if (skill) {
      const result = writeSkill(targetRoot, { dryRun: true, global });
      process.stdout.write(
        result.written
          ? `Would write ${result.file}\n`
          : `Would leave existing ${result.file}\n`
      );
    }

    return;
  }

  writeConfig(file, config);
  process.stdout.write(`roslyngate: wrote ${file}\n`);

  if (skill) {
    const result = writeSkill(targetRoot, { global });
    process.stdout.write(
      result.written
        ? `roslyngate: wrote ${result.file}\n`
        : `roslyngate: left existing ${result.file}\n`
    );
  }

  process.stdout.write("Next: open OpenCode and run /mcps to confirm roslyngate.\n");
}

module.exports = {
  SCHEMA,
  SERVER_NAME,
  buildEntry,
  configFile,
  configRoot,
  mergeConfig,
  parseArgs,
  runSetup,
  writeConfig
};
