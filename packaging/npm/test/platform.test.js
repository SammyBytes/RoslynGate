const test = require("node:test");
const assert = require("node:assert/strict");
const { assetName, resolveRid } = require("../src/platform");

test("maps supported platforms to RIDs", () => {
  assert.equal(resolveRid("linux", "x64"), "linux-x64");
  assert.equal(resolveRid("win32", "x64"), "win-x64");
  assert.equal(resolveRid("darwin", "arm64"), "osx-arm64");
});

test("rejects unsupported platforms", () => {
  assert.throws(() => resolveRid("freebsd", "x64"), /Unsupported platform/);
});

test("builds release asset names", () => {
  assert.equal(assetName("linux-x64"), "roslyngate-linux-x64");
  assert.equal(assetName("win-x64"), "roslyngate-win-x64.exe");
  assert.equal(assetName("osx-arm64"), "roslyngate-osx-arm64");
});
