const test = require("node:test");
const assert = require("node:assert/strict");
const {
  detectPackageManager,
  fetchLatestVersion,
  installLatest,
  isNewer
} = require("../src/upgrade-npm");

function latestFetch(version) {
  return async () => ({
    ok: true,
    status: 200,
    json: async () => ({ version })
  });
}

test("compares versions without downgrading", () => {
  assert.equal(isNewer("0.1.5", "0.1.4"), true);
  assert.equal(isNewer("0.1.4", "0.1.4"), false);
  assert.equal(isNewer("0.1.3", "0.1.4"), false);
  assert.equal(isNewer("0.2.0", "0.1.9"), true);
});

test("runs npm through cmd.exe on Windows without shell mode", async () => {
  const { EventEmitter } = require("node:events");
  let captured;

  await installLatest("0.1.5", {
    platform: "win32",
    env: { ComSpec: "C:\\Windows\\System32\\cmd.exe" },
    spawnImpl: (command, args, options) => {
      captured = { command, args, options };
      const child = new EventEmitter();
      setImmediate(() => child.emit("exit", 0, null));
      return child;
    }
  });

  assert.equal(captured.command, "C:\\Windows\\System32\\cmd.exe");
  assert.deepEqual(captured.args, [
    "/d",
    "/s",
    "/c",
    "npm install -g roslyngate@0.1.5"
  ]);
  assert.equal(captured.options.shell, undefined);
});

test("runs bun directly on Windows", async () => {
  const { EventEmitter } = require("node:events");
  let captured;

  await installLatest("0.1.5", {
    manager: "bun",
    platform: "win32",
    spawnImpl: (command, args) => {
      captured = { command, args };
      const child = new EventEmitter();
      setImmediate(() => child.emit("exit", 0, null));
      return child;
    }
  });

  assert.equal(captured.command, "bun.exe");
  assert.deepEqual(captured.args, ["install", "-g", "roslyngate@0.1.5"]);
});

test("detects bun from its global install path", () => {
  const env = { BUN_INSTALL: "C:\\Users\\me\\.bun" };

  assert.equal(detectPackageManager({
    env,
    scriptPath: "C:\\Users\\me\\.bun\\install\\global\\node_modules\\roslyngate\\bin\\roslyngate.js"
  }), "bun");
  assert.equal(detectPackageManager({
    env,
    scriptPath: "C:\\Users\\me\\AppData\\Roaming\\npm\\node_modules\\roslyngate\\bin\\roslyngate.js"
  }), "npm");
});

test("reads the latest version from npm", async () => {
  const version = await fetchLatestVersion({ fetchImpl: latestFetch("0.2.0") });
  assert.equal(version, "0.2.0");
});
