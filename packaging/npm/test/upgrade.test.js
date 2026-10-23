const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const assert = require("node:assert/strict");
const { binaryPath } = require("../src/cache");
const {
  fetchLatestVersion,
  installLatest,
  isNewer,
  parseUpgradeArgs,
  runUpgrade,
  updateConfig
} = require("../src/upgrade");

function temporaryDirectory() {
  return fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-upgrade-"));
}

function latestFetch(version) {
  return async () => ({
    ok: true,
    status: 200,
    json: async () => ({ version })
  });
}

function writeMcpConfig(root) {
  const file = path.join(root, ".mcp.json");
  fs.writeFileSync(file, JSON.stringify({
    timeout: 1800000,
    mcpServers: {
      roslyngate: {
        command: "/old/roslyngate",
        args: ["--solution", "repo.sln", "--no-prewarm"],
        timeout: 900000
      },
      other: { command: "keep-me" }
    }
  }));
  return file;
}

test("parses upgrade options", () => {
  assert.deepEqual(parseUpgradeArgs(["upgrade"]), {
    check: false,
    dryRun: false,
    help: false
  });
  assert.deepEqual(parseUpgradeArgs(["upgrade", "--check"]), {
    check: true,
    dryRun: false,
    help: false
  });
  assert.deepEqual(parseUpgradeArgs(["upgrade", "--dry-run"]), {
    check: false,
    dryRun: true,
    help: false
  });
  assert.throws(() => parseUpgradeArgs(["upgrade", "--wat"]), /Unknown upgrade option/);
});

test("compares versions without downgrading", () => {
  assert.equal(isNewer("0.1.5", "0.1.4"), true);
  assert.equal(isNewer("0.1.4", "0.1.4"), false);
  assert.equal(isNewer("0.1.3", "0.1.4"), false);
  assert.equal(isNewer("0.2.0", "0.1.9"), true);
});

test("uses a shell for npm.cmd on Windows", async () => {
  const { EventEmitter } = require("node:events");
  let captured;

  await installLatest("0.1.5", {
    platform: "win32",
    spawnImpl: (command, args, options) => {
      captured = { command, args, options };
      const child = new EventEmitter();
      setImmediate(() => child.emit("exit", 0, null));
      return child;
    }
  });

  assert.equal(captured.command, "npm.cmd");
  assert.deepEqual(captured.args, ["install", "-g", "roslyngate@0.1.5"]);
  assert.equal(captured.options.shell, true);
});

test("reads the latest version from npm", async () => {
  const version = await fetchLatestVersion({ fetchImpl: latestFetch("0.2.0") });
  assert.equal(version, "0.2.0");
});

test("updates only the roslyngate command and preserves the rest", () => {
  const root = temporaryDirectory();
  const file = writeMcpConfig(root);

  const result = updateConfig(root, "/new/roslyngate");

  assert.equal(result.updated, true);
  const config = JSON.parse(fs.readFileSync(file, "utf8"));
  assert.equal(config.timeout, 1800000);
  assert.equal(config.mcpServers.roslyngate.command, "/new/roslyngate");
  assert.deepEqual(config.mcpServers.roslyngate.args, [
    "--solution",
    "repo.sln",
    "--no-prewarm"
  ]);
  assert.equal(config.mcpServers.roslyngate.timeout, 900000);
  assert.equal(config.mcpServers.other.command, "keep-me");
});

test("installs the latest version and refreshes the config", async () => {
  const root = temporaryDirectory();
  const cache = temporaryDirectory();
  const file = writeMcpConfig(root);
  const installed = [];
  let downloaded;

  await runUpgrade([], {
    version: "0.1.4",
    cwd: root,
    env: { ROSLYGATE_CACHE_DIR: cache },
    platform: "linux",
    arch: "x64",
    fetchImpl: latestFetch("0.1.5"),
    install: async (version) => installed.push(version),
    ensureBinaryImpl: async ({ target }) => {
      downloaded = target;
      return target;
    }
  });

  assert.deepEqual(installed, ["0.1.5"]);
  assert.equal(
    downloaded,
    binaryPath({
      version: "0.1.5",
      asset: "roslyngate-linux-x64",
      env: { ROSLYGATE_CACHE_DIR: cache },
      platform: "linux"
    })
  );

  const config = JSON.parse(fs.readFileSync(file, "utf8"));
  assert.equal(config.mcpServers.roslyngate.command, downloaded);
});

test("does not downgrade when the installed version is newer", async () => {
  const root = temporaryDirectory();
  const cache = temporaryDirectory();
  const file = writeMcpConfig(root);
  let downloadedVersion;

  await runUpgrade([], {
    version: "0.1.5",
    cwd: root,
    env: { ROSLYGATE_CACHE_DIR: cache },
    platform: "linux",
    arch: "x64",
    fetchImpl: latestFetch("0.1.4"),
    install: async () => {
      throw new Error("should not install");
    },
    ensureBinaryImpl: async ({ version, target }) => {
      downloadedVersion = version;
      return target;
    }
  });

  assert.equal(downloadedVersion, "0.1.5");
  const config = JSON.parse(fs.readFileSync(file, "utf8"));
  assert.match(config.mcpServers.roslyngate.command, /0\.1\.5\/roslyngate-linux-x64$/);
});

test("--check does not install or write", async () => {
  const root = temporaryDirectory();
  const file = writeMcpConfig(root);
  const before = fs.readFileSync(file, "utf8");
  let installed = false;

  await runUpgrade(["upgrade", "--check"], {
    version: "0.1.4",
    cwd: root,
    platform: "linux",
    arch: "x64",
    fetchImpl: latestFetch("0.1.5"),
    install: async () => { installed = true; },
    ensureBinaryImpl: async () => {
      throw new Error("should not download");
    }
  });

  assert.equal(installed, false);
  assert.equal(fs.readFileSync(file, "utf8"), before);
});
