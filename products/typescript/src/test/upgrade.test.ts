import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import {
  detectPackageManager,
  isNewer,
  parseUpgradeArgs,
  runUpgrade
} from "../upgrade.js";

function temporary(): string {
  return fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-ts-upgrade-"));
}

function fakeFetch(version: string): typeof fetch {
  return (async () => ({
    ok: true,
    json: async () => ({ version })
  })) as unknown as typeof fetch;
}

test("parses upgrade options", () => {
  assert.deepEqual(parseUpgradeArgs(["--check", "--manager", "bun"]), {
    check: true,
    dryRun: false,
    noConfig: false,
    manager: "bun",
    help: false
  });
  assert.throws(() => parseUpgradeArgs(["--manager", "pnpm"]), /npm or bun/);
});

test("compares versions and detects bun", () => {
  assert.equal(isNewer("0.4.0", "0.3.9"), true);
  assert.equal(isNewer("0.3.0", "0.3.0"), false);
  assert.equal(
    detectPackageManager({ env: { BUN_INSTALL: "/home/me/.bun" }, scriptPath: "/home/me/.bun/bin/x" }),
    "bun"
  );
  assert.equal(detectPackageManager({ env: {}, scriptPath: "/usr/lib/node_modules/x" }), "npm");
});

test("updates the package and refreshes managed MCP entries", async () => {
  const root = temporary();
  const oldCli = path.join(root, "cache", "roslyngate-ts", "dist", "cli.js");
  const newCli = path.join(root, "global", "roslyngate-ts", "dist", "cli.js");
  fs.writeFileSync(
    path.join(root, ".mcp.json"),
    `${JSON.stringify(
      { mcpServers: { "roslyngate-ts": { command: "node", args: [oldCli, "--solution", root] } } },
      null,
      2
    )}\n`
  );

  let installed = "";
  try {
    await runUpgrade([], {
      version: "0.3.0",
      cwd: root,
      cliPath: newCli,
      fetchImpl: fakeFetch("0.4.0"),
      installImpl: async (version) => {
        installed = version;
      }
    });

    assert.equal(installed, "0.4.0");
    const config = JSON.parse(fs.readFileSync(path.join(root, ".mcp.json"), "utf8"));
    assert.deepEqual(config.mcpServers["roslyngate-ts"].args, [
      newCli,
      "--solution",
      root
    ]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("check mode reports versions without installing", async () => {
  let installed = false;
  await runUpgrade(["--check"], {
    version: "0.3.0",
    fetchImpl: fakeFetch("0.3.1"),
    installImpl: async () => {
      installed = true;
    }
  });
  assert.equal(installed, false);
});
