const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const assert = require("node:assert/strict");
const {
  buildEntry,
  mergeConfig,
  parseArgs,
  resolveSolution,
  runSetup,
  writeConfig
} = require("../src/setup");
const { skillPath, writeSkill } = require("../src/skill");

function temporaryDirectory() {
  return fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-setup-"));
}

test("parses setup options", () => {
  assert.deepEqual(parseArgs(["claude"]), {
    solution: ".",
    print: false,
    dryRun: false,
    skill: false
  });
  assert.deepEqual(parseArgs(["claude", "--solution", "repo", "--print"]), {
    solution: "repo",
    print: true,
    dryRun: false,
    skill: false
  });
  assert.deepEqual(parseArgs(["claude", "--solution=repo", "--dry-run"]), {
    solution: "repo",
    print: false,
    dryRun: true,
    skill: false
  });
  assert.deepEqual(parseArgs(["claude", "--skill"]), {
    solution: ".",
    print: false,
    dryRun: false,
    skill: true
  });
});

test("rejects unknown setup options", () => {
  assert.throws(() => parseArgs(["claude", "--wat"]), /Unknown setup option/);
});

test("resolves a directory or solution file", () => {
  const root = temporaryDirectory();
  const solution = path.join(root, "Sample.sln");
  fs.writeFileSync(solution, "");

  try {
    assert.deepEqual(resolveSolution(root), { solution: root, root });
    assert.deepEqual(resolveSolution(solution), { solution, root });
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("builds the Claude entry", () => {
  assert.deepEqual(buildEntry("/tmp/roslyngate", "/tmp/repo"), {
    command: "/tmp/roslyngate",
    args: ["--solution", "/tmp/repo"]
  });
});

test("preserves other MCP servers and top-level keys", () => {
  const config = mergeConfig(
    {
      mcpServers: { other: { command: "other" } },
      custom: true
    },
    buildEntry("/tmp/roslyngate", "/tmp/repo")
  );

  assert.equal(config.custom, true);
  assert.equal(config.mcpServers.other.command, "other");
  assert.equal(config.mcpServers.roslyngate.command, "/tmp/roslyngate");
});

test("rejects invalid mcpServers", () => {
  assert.throws(
    () => mergeConfig({ mcpServers: [] }, buildEntry("bin", "repo")),
    /invalid mcpServers/
  );
});

test("writes and merges .mcp.json", () => {
  const root = temporaryDirectory();
  const file = path.join(root, ".mcp.json");
  fs.writeFileSync(
    file,
    JSON.stringify({ mcpServers: { other: { command: "other" } } })
  );

  try {
    writeConfig(root, buildEntry("/tmp/roslyngate", "/tmp/repo"));
    const config = JSON.parse(fs.readFileSync(file, "utf8"));
    assert.equal(config.mcpServers.other.command, "other");
    assert.equal(config.mcpServers.roslyngate.command, "/tmp/roslyngate");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("rejects malformed existing config", () => {
  const root = temporaryDirectory();
  fs.writeFileSync(path.join(root, ".mcp.json"), "{not json");

  try {
    assert.throws(
      () => writeConfig(root, buildEntry("bin", "repo")),
      /not valid JSON/
    );
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("setup writes config without downloading when a binary is provided", async () => {
  const root = temporaryDirectory();
  const binary = path.join(root, "roslyngate");
  fs.writeFileSync(binary, "");

  try {
    await runSetup(["claude"], {
      version: "0.2.0",
      cwd: root,
      env: { ROSLYGATE_BINARY: binary }
    });

    const config = JSON.parse(fs.readFileSync(path.join(root, ".mcp.json"), "utf8"));
    assert.equal(config.mcpServers.roslyngate.command, binary);
    assert.deepEqual(config.mcpServers.roslyngate.args, ["--solution", root]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("writes the opt-in skill once", () => {
  const root = temporaryDirectory();
  const file = skillPath(root);

  try {
    const first = writeSkill(root);
    assert.equal(first.written, true);
    assert.match(fs.readFileSync(file, "utf8"), /name: roslyn-symbols/);

    fs.writeFileSync(file, "custom skill");
    const second = writeSkill(root);
    assert.equal(second.written, false);
    assert.equal(fs.readFileSync(file, "utf8"), "custom skill");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("setup --skill writes the skill without downloading", async () => {
  const root = temporaryDirectory();
  const binary = path.join(root, "roslyngate");
  fs.writeFileSync(binary, "");

  try {
    await runSetup(["claude", "--skill"], {
      version: "0.2.0",
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
