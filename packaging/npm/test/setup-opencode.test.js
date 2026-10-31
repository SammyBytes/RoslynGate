const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const assert = require("node:assert/strict");
const {
  buildEntry,
  configFile,
  mergeConfig,
  parseArgs,
  runSetup
} = require("../src/setup-opencode");
const { readConfig, stripJsonc } = require("../src/jsonc");
const { skillPath } = require("../src/opencode-skill");

function temporaryDirectory() {
  return fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-opencode-"));
}

test("parses opencode setup options", () => {
  assert.deepEqual(parseArgs(["opencode"]), {
    solution: ".",
    print: false,
    dryRun: false,
    global: false,
    skill: false
  });
  assert.deepEqual(parseArgs(["opencode", "--solution=repo", "--global", "--skill"]), {
    solution: "repo",
    print: false,
    dryRun: false,
    global: true,
    skill: true
  });
});

test("parses JSONC comments and trailing commas", () => {
  const parsed = JSON.parse(stripJsonc(`{
    // comment
    "mcp": { "servers": { "other": { "type": "local" } }, },
    /* block */
  }`));

  assert.equal(parsed.mcp.servers.other.type, "local");
});

test("builds a local OpenCode server entry", () => {
  assert.deepEqual(buildEntry("/tmp/roslyngate", "/tmp/repo"), {
    type: "local",
    command: ["/tmp/roslyngate", "--solution", "/tmp/repo"],
    codemode: false,
    timeout: { startup: 60000, catalog: 60000 }
  });
});

test("merges without removing other servers", () => {
  const config = mergeConfig(
    { mcp: { servers: { other: { type: "local" } } }, model: "sonnet" },
    buildEntry("bin", "repo")
  );

  assert.equal(config.model, "sonnet");
  assert.equal(config.mcp.servers.other.type, "local");
  assert.equal(config.mcp.servers.roslyngate.type, "local");
});

test("writes opencode.jsonc with the roslyngate server", async () => {
  const root = temporaryDirectory();
  const binary = path.join(root, "roslyngate");
  fs.writeFileSync(binary, "");

  try {
    await runSetup(["opencode", "--skill", "--solution", root], {
      version: "0.3.0",
      cwd: root,
      env: { ROSLYGATE_BINARY: binary }
    });

    const file = path.join(root, "opencode.jsonc");
    assert.equal(configFile(root), file);
    const config = JSON.parse(fs.readFileSync(file, "utf8"));
    assert.equal(config.$schema, "https://opencode.ai/config.json");
    assert.deepEqual(config.mcp.servers.roslyngate.command, [
      binary,
      "--solution",
      root
    ]);
    assert.equal(config.mcp.servers.roslyngate.codemode, false);
    assert.ok(config.instructions.includes(".opencode/skills/roslyn-symbols/SKILL.md"));
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("preserves an existing OpenCode config", async () => {
  const root = temporaryDirectory();
  const binary = path.join(root, "roslyngate");
  const file = path.join(root, "opencode.json");
  fs.writeFileSync(binary, "");
  fs.writeFileSync(file, JSON.stringify({
    model: "sonnet",
    mcp: { servers: { other: { type: "local" } } }
  }));

  try {
    await runSetup(["opencode"], {
      version: "0.3.0",
      cwd: root,
      env: { ROSLYGATE_BINARY: binary }
    });

    const config = readConfig(file);
    assert.equal(config.model, "sonnet");
    assert.equal(config.mcp.servers.other.type, "local");
    assert.equal(config.mcp.servers.roslyngate.type, "local");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("writes the opt-in RoslynGate skill", async () => {
  const root = temporaryDirectory();
  const binary = path.join(root, "roslyngate");
  fs.writeFileSync(binary, "");

  try {
    await runSetup(["opencode", "--skill"], {
      version: "0.3.0",
      cwd: root,
      env: { ROSLYGATE_BINARY: binary }
    });

    const file = skillPath(root);
    assert.equal(fs.existsSync(file), true);
    assert.match(fs.readFileSync(file, "utf8"), /includeDefinition=true/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
