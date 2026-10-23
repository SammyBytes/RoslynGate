const test = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");
const { binaryPath, cacheRoot } = require("../src/cache");

test("uses XDG_CACHE_HOME on Unix", () => {
  assert.equal(
    cacheRoot({ XDG_CACHE_HOME: "/tmp/cache" }, "linux"),
    path.join("/tmp/cache", "roslyngate")
  );
});

test("honors the explicit cache override", () => {
  assert.equal(
    cacheRoot({ ROSLYGATE_CACHE_DIR: "/tmp/custom" }, "linux"),
    "/tmp/custom"
  );
});

test("uses LOCALAPPDATA on Windows", () => {
  assert.equal(
    cacheRoot({ LOCALAPPDATA: "C:\\Temp" }, "win32"),
    path.join("C:\\Temp", "roslyngate")
  );
});

test("places binaries under version and asset", () => {
  const target = binaryPath({
    version: "0.1.0",
    asset: "roslyngate-linux-x64",
    env: { ROSLYGATE_CACHE_DIR: "/tmp/custom" },
    platform: "linux"
  });

  assert.equal(target, path.join("/tmp/custom", "0.1.0", "roslyngate-linux-x64"));
});
