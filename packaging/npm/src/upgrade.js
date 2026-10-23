const path = require("node:path");
const { assetName, resolveRid } = require("./platform");
const { binaryPath } = require("./cache");
const { ensureBinary } = require("./download");
const { CONFIG_FILE, SERVER_NAME, updateConfig } = require("./upgrade-config");
const { fetchLatestVersion, installLatest, isNewer } = require("./upgrade-npm");

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

  if (newer) {
    if (parsed.dryRun) {
      process.stdout.write(`Would run npm install -g roslyngate@${latest}.\n`);
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
  parseUpgradeArgs,
  runUpgrade
};
