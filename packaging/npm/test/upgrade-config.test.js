const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const assert = require("node:assert/strict");
const { updateConfig } = require("../src/upgrade-config");
const { isManagedCommand } = require("../src/upgrade");

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

test("updates only the roslyngate command and preserves the rest", () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-config-"));
  const file = writeMcpConfig(root);

  try {
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
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("leaves an unmanaged command alone", () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-config-"));
  const file = writeMcpConfig(root);
  const before = fs.readFileSync(file, "utf8");

  try {
    const result = updateConfig(root, "/new/roslyngate", {
      canUpdate: () => false
    });

    assert.equal(result.updated, false);
    assert.equal(result.reason, "unmanaged");
    assert.equal(fs.readFileSync(file, "utf8"), before);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("detects managed cache commands only", () => {
  const cache = fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-cache-"));
  const env = { ROSLYGATE_CACHE_DIR: cache };

  assert.equal(
    isManagedCommand(path.join(cache, "0.1.4", "roslyngate-linux-x64"), { env, platform: "linux" }),
    true
  );
  assert.equal(isManagedCommand("roslyngate", { env, platform: "linux" }), false);
  assert.equal(isManagedCommand("npx", { env, platform: "linux" }), false);
});
