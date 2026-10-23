const fs = require("node:fs");
const path = require("node:path");
const { spawn } = require("node:child_process");
const { assetName, resolveRid } = require("./platform");
const { binaryPath } = require("./cache");
const { ensureBinary } = require("./download");

const PACKAGE = "roslyngate";
const CONFIG_FILE = ".mcp.json";
const SERVER_NAME = "roslyngate";
const REGISTRY = "https://registry.npmjs.org";

function parseUpgradeArgs(args) {
  let check = false;
  let dryRun = false;
  let help = false;

  for (const argument of args) {
    if (argument === "upgrade") {
      continue;
    }

    if (argument === "--check") {
      check = true;
      continue;
    }

    if (argument === "--dry-run") {
      dryRun = true;
      continue;
    }

    if (argument === "--help" || argument === "-h") {
      help = true;
      continue;
    }

    throw new Error(`Unknown upgrade option: ${argument}`);
  }

  return { check, dryRun, help };
}

function isNewer(candidate, current) {
  const parse = (value) => value.split("-")[0].split(".").map(Number);
  const left = parse(candidate);
  const right = parse(current);

  for (let index = 0; index < Math.max(left.length, right.length); index += 1) {
    const difference = (left[index] || 0) - (right[index] || 0);
    if (difference !== 0) {
      return difference > 0;
    }
  }

  return false;
}

async function fetchLatestVersion({ fetchImpl = globalThis.fetch, registry = REGISTRY } = {}) {
  const response = await fetchImpl(`${registry}/${PACKAGE}/latest`, {
    redirect: "follow",
    headers: { accept: "application/json" }
  });

  if (!response.ok) {
    throw new Error(`Could not query npm (${response.status}).`);
  }

  const data = await response.json();
  if (!data || typeof data.version !== "string") {
    throw new Error("npm response has no version.");
  }

  return data.version;
}

function installLatest(version, { platform = process.platform, spawnImpl = spawn } = {}) {
  const command = platform === "win32" ? "npm.cmd" : "npm";
  const useShell = platform === "win32";

  return new Promise((resolve, reject) => {
    const child = spawnImpl(command, ["install", "-g", `${PACKAGE}@${version}`], {
      stdio: "inherit",
      shell: useShell
    });

    child.on("error", reject);
    child.on("exit", (code, signal) => {
      if (signal) {
        reject(new Error(`npm install stopped by ${signal}.`));
        return;
      }

      if (code !== 0) {
        reject(new Error(`npm install exited with code ${code}.`));
        return;
      }

      resolve();
    });
  });
}

function updateConfig(root, binary) {
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

  entry.command = binary;
  const temporary = `${file}.${process.pid}.tmp`;
  fs.writeFileSync(temporary, `${JSON.stringify(config, null, 2)}\n`);
  fs.renameSync(temporary, file);

  return { updated: true, file };
}

async function runUpgrade(args, options = {}) {
  const {
    version,
    cwd = process.cwd(),
    env = process.env,
    platform = process.platform,
    arch = process.arch,
    fetchImpl,
    spawnImpl,
    ensureBinaryImpl = ensureBinary
  } = options;

  const parsed = parseUpgradeArgs(args);
  if (parsed.help) {
    process.stdout.write(
      "Usage: roslyngate upgrade [--check] [--dry-run]\n" +
      "Updates the global npm package, downloads the matching binary, and\n" +
      "refreshes the roslyngate entry in .mcp.json in the current directory.\n"
    );
    return;
  }

  const latest = await fetchLatestVersion({
    fetchImpl,
    registry: env.ROSLYGATE_REGISTRY || REGISTRY
  });

  if (parsed.check) {
    process.stdout.write(`roslyngate ${version}; latest ${latest}.\n`);
    return;
  }

  const asset = assetName(resolveRid(platform, arch));
  const newer = isNewer(latest, version);
  const targetVersion = newer ? latest : version;
  const target = binaryPath({ version: targetVersion, asset, env, platform });

  if (newer) {
    if (parsed.dryRun) {
      process.stdout.write(`Would run npm install -g ${PACKAGE}@${latest}.\n`);
    } else {
      process.stdout.write(`roslyngate: updating ${version} -> ${latest}...\n`);
      await (options.install || installLatest)(latest, { platform, spawnImpl });
    }
  } else {
    process.stdout.write(`roslyngate: ${version} is up to date.\n`);
  }

  if (parsed.dryRun) {
    process.stdout.write(`Would download ${target} and refresh ${path.join(cwd, CONFIG_FILE)}.\n`);
    return;
  }

  const binary = await ensureBinaryImpl({ asset, version: targetVersion, target });
  const result = updateConfig(cwd, binary);

  if (result.updated) {
    process.stdout.write(`roslyngate: updated ${result.file}\n`);
  } else if (result.reason === "missing") {
    process.stdout.write(`roslyngate: no ${CONFIG_FILE} here; run roslyngate setup claude --solution <path>.\n`);
  } else {
    process.stdout.write(`roslyngate: ${result.file} has no ${SERVER_NAME} server; run setup.\n`);
  }

  process.stdout.write(`roslyngate: ${targetVersion} is ready. Restart Claude Code or run /mcp.\n`);
}

module.exports = {
  CONFIG_FILE,
  SERVER_NAME,
  fetchLatestVersion,
  installLatest,
  isNewer,
  parseUpgradeArgs,
  runUpgrade,
  updateConfig
};
