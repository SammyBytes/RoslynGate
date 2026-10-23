const test = require("node:test");
const assert = require("node:assert/strict");
const { installLatest } = require("../src/upgrade-npm");
const { fetchLatestVersion } = require("../src/upgrade-npm");
const { isNewer } = require("../src/upgrade-npm");

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
