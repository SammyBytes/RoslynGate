const path = require("node:path");
const { assetName, resolveRid } = require("./platform");
const { binaryPath, cacheRoot } = require("./cache");
const { ensureBinary } = require("./download");
const { CONFIG_FILE, SERVER_NAME, updateConfig } = require("./upgrade-config");
const {
  PACKAGE,
  detectPackageManager,
  fetchLatestVersion,
  installLatest,
  isNewer
} = require("./upgrade-npm");

function parseUpgradeArgs(args) {
  let check = false;
  let dryRun = false;
  let noConfig = false;
  let manager;
  let help = false;

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];

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

    if (argument === "--no-config") {
      noConfig = true;
      continue;
    }

    if (argument === "--manager") {
      const value = args[++index];
      if (value !== "npm" && value !== "bun") {
        throw new Error("--manager must be npm or bun.");
      }
      manager = value;
      continue;
    }

    if (argument === "--help" || argument === "-h") {
      help = true;
      continue;
    }

    throw new Error(`Unknown upgrade option: ${argument}`);
  }

  return { check, dryRun, noConfig, manager, help };
}

function isManagedCommand(command, { env, platform }) {
  if (typeof command !== "string" || command.length === 0) {
    return false;
  }

  const root = path.resolve(cacheRoot(env, platform)) + path.sep;
  const candidate = path.resolve(command);
  const normalize = platform === "win32"
    ? (value) => value.toLowerCase()
    : (value) => value;

  return normalize(candidate).startsWith(normalize(root));
}

async function runUpgrade(args, options = {}) {
  const {
    version,
    cwd = process.cwd(),
    env = process.env,
    platform = process.platform,
    arch = process.arch,
    packageManager,
    scriptPath,
    fetchImpl,
    spawnImpl,
    ensureBinaryImpl = ensureBinary
  } = options;

  const parsed = parseUpgradeArgs(args);
  if (parsed.help) {
    process.stdout.write(
      "Usage: roslyngate upgrade [--check] [--dry-run] [--no-config] [--manager npm|bun]\n" +
      "Updates the global package and matching binary. Unless --no-config\n" +
      "is set, refreshes a managed roslyngate entry in .mcp.json.\n"
    );
    return;
  }

  const latest = await fetchLatestVersion({
    fetchImpl,
    registry: env.ROSLYGATE_REGISTRY
  });

  if (parsed.check) {
    process.stdout.write(`roslyngate ${version}; latest ${latest}.\n`);
    return;
  }

  const asset = assetName(resolveRid(platform, arch));
  const newer = isNewer(latest, version);
  const targetVersion = newer ? latest : version;
  const target = binaryPath({ version: targetVersion, asset, env, platform });
  const manager = parsed.manager || packageManager || detectPackageManager({ env, scriptPath });

  if (newer) {
    if (parsed.dryRun) {
      process.stdout.write(`Would run ${manager} install -g ${PACKAGE}@${latest}.\n`);
    } else {
      process.stdout.write(`roslyngate: updating ${version} -> ${latest} with ${manager}...\n`);
      await (options.install || installLatest)(latest, {
        manager,
        platform,
        spawnImpl,
        env
      });
    }
  } else {
    process.stdout.write(`roslyngate: ${version} is up to date.\n`);
  }

  if (parsed.dryRun) {
    if (parsed.noConfig) {
      process.stdout.write(`Would download ${target} and skip .mcp.json.\n`);
    } else {
      process.stdout.write(`Would download ${target} and refresh ${path.join(cwd, CONFIG_FILE)}.\n`);
    }
    return;
  }

  const binary = await ensureBinaryImpl({ asset, version: targetVersion, target });

  if (parsed.noConfig) {
    process.stdout.write("roslyngate: skipped .mcp.json because --no-config is set.\n");
  } else {
    const result = updateConfig(cwd, binary, {
      canUpdate: (command) => isManagedCommand(command, { env, platform })
    });

    if (result.updated) {
      process.stdout.write(`roslyngate: updated ${result.file}\n`);
    } else if (result.reason === "missing") {
      process.stdout.write(`roslyngate: no ${CONFIG_FILE} here; run roslyngate setup claude --solution <path>.\n`);
    } else if (result.reason === "unmanaged") {
      process.stdout.write(`roslyngate: left ${result.file} unchanged; its command is not managed by RoslynGate.\n`);
    } else {
      process.stdout.write(`roslyngate: ${result.file} has no ${SERVER_NAME} server; run setup.\n`);
    }
  }

  process.stdout.write(`roslyngate: ${targetVersion} is ready. Restart Claude Code or run /mcp.\n`);
}

module.exports = {
  isManagedCommand,
  parseUpgradeArgs,
  runUpgrade
};
