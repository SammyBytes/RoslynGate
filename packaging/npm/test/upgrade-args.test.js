const test = require("node:test");
const assert = require("node:assert/strict");
const { parseUpgradeArgs } = require("../src/upgrade");

test("parses upgrade options", () => {
  assert.deepEqual(parseUpgradeArgs(["upgrade"]), {
    check: false,
    dryRun: false,
    noConfig: false,
    manager: undefined,
    help: false
  });
  assert.deepEqual(parseUpgradeArgs(["upgrade", "--check"]), {
    check: true,
    dryRun: false,
    noConfig: false,
    manager: undefined,
    help: false
  });
  assert.deepEqual(parseUpgradeArgs(["upgrade", "--dry-run"]), {
    check: false,
    dryRun: true,
    noConfig: false,
    manager: undefined,
    help: false
  });
  assert.deepEqual(parseUpgradeArgs(["upgrade", "--no-config"]), {
    check: false,
    dryRun: false,
    noConfig: true,
    manager: undefined,
    help: false
  });
  assert.deepEqual(parseUpgradeArgs(["upgrade", "--manager", "bun"]), {
    check: false,
    dryRun: false,
    noConfig: false,
    manager: "bun",
    help: false
  });
  assert.throws(
    () => parseUpgradeArgs(["upgrade", "--manager", "pnpm"]),
    /--manager must be npm or bun/
  );
  assert.throws(() => parseUpgradeArgs(["upgrade", "--wat"]), /Unknown upgrade option/);
});
