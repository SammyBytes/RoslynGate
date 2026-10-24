#!/usr/bin/env node
const { spawn } = require("node:child_process");
const fs = require("node:fs");
const path = require("node:path");
const pkg = require("../packaging/npm/package.json");
const { assetName, resolveRid } = require("../packaging/npm/src/platform");
const { binaryPath } = require("../packaging/npm/src/cache");
const { backupConfig, withConfig, withoutConfig, writeConfig } = require("./opencode-ab-config");
const { median, parseOutput } = require("./opencode-ab-output");

function parseArgs(argv) {
  const options = {
    solution: process.cwd(),
    prompt: null,
    symbols: [],
    runs: 1,
    model: "opencode-go/deepseek-v4.1-flash",
    binary: null,
    timeoutMs: 900000
  };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    const value = argv[index + 1];

    if (argument === "--solution") {
      options.solution = path.resolve(value);
      index += 1;
    } else if (argument === "--prompt") {
      options.prompt = value;
      index += 1;
    } else if (argument === "--symbols") {
      options.symbols = value.split(",").map((item) => item.trim()).filter(Boolean);
      index += 1;
    } else if (argument === "--runs") {
      options.runs = Number.parseInt(value, 10);
      index += 1;
    } else if (argument === "--model") {
      options.model = value;
      index += 1;
    } else if (argument === "--binary") {
      options.binary = path.resolve(value);
      index += 1;
    } else if (argument === "--timeout-ms") {
      options.timeoutMs = Number.parseInt(value, 10);
      index += 1;
    } else if (argument === "--help" || argument === "-h") {
      printHelp();
      process.exit(0);
    } else {
      throw new Error(`Unknown option: ${argument}`);
    }
  }

  if (options.runs < 1) {
    throw new Error("--runs must be at least 1.");
  }

  if (!options.prompt && options.symbols.length === 0) {
    throw new Error("Pass --prompt or --symbols.");
  }

  return options;
}

function printHelp() {
  process.stdout.write(`Usage: node benchmarks/opencode-ab.js --solution <dir> [options]

Required:
  --solution <dir>       Repository/solution directory.
  --symbols a,b,c        Symbols to inspect, or --prompt.
  --prompt <text>        Custom prompt.
  --binary <path>        RoslynGate binary (otherwise from config).

Options:
  --runs <n>             Runs per mode. Default 1.
  --model <provider/id>  OpenCode model. Default opencode-go/deepseek-v4.1-flash.
  --timeout-ms <n>       Per-run timeout. Default 900000.
`);
}

function buildPrompt(options) {
  if (options.prompt) {
    return options.prompt;
  }

  return `Use the available tools. For each symbol below, find its definition and all semantic references. Answer only with file:line entries. Do not guess.\nSymbols: ${options.symbols.join(", ")}`;
}

async function runOpenCode(options, prompt, config, { hideSkill = false } = {}) {
  const restore = backupConfig(options.solution, { hideSkill });
  try {
    writeConfig(options.solution, config);
    return await new Promise((resolve, reject) => {
      const child = spawn(
        process.platform === "win32" ? "opencode.cmd" : "opencode",
        [
          "run",
          "--standalone",
          "--auto",
          "--format",
          "json",
          "--model",
          options.model,
          prompt
        ],
        {
          cwd: options.solution,
          stdio: ["ignore", "pipe", "pipe"],
          shell: process.platform === "win32"
        }
      );

      let stdout = "";
      let stderr = "";
      const timer = setTimeout(() => {
        child.kill("SIGTERM");
        reject(new Error("OpenCode timed out."));
      }, options.timeoutMs);

      child.stdout.on("data", (chunk) => { stdout += chunk; });
      child.stderr.on("data", (chunk) => { stderr += chunk; });
      child.on("error", (error) => {
        clearTimeout(timer);
        reject(error);
      });
      child.on("exit", (code) => {
        clearTimeout(timer);
        if (code !== 0) {
          reject(new Error(`OpenCode exited with ${code}: ${stderr.slice(-800)}`));
          return;
        }

        resolve(parseOutput(stdout));
      });
    });
  } finally {
    restore();
  }
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  const rid = resolveRid(process.platform, process.arch);
  const binary = options.binary
    || process.env.ROSLYGATE_BINARY
    || binaryPath({ version: pkg.version, asset: assetName(rid) });

  if (!fs.existsSync(binary)) {
    throw new Error(`RoslynGate binary not found: ${binary}. Pass --binary.`);
  }

  const prompt = buildPrompt(options);
  const runs = [];

  for (let run = 1; run <= options.runs; run += 1) {
    const order = run % 2 === 1 ? ["with", "without"] : ["without", "with"];
    for (const mode of order) {
      const started = Date.now();
      const config = mode === "with"
        ? withConfig(binary, options.solution)
        : withoutConfig();
      const result = await runOpenCode(options, prompt, config, {
        hideSkill: mode === "without"
      });
      runs.push({ run, mode, ...result, durationMs: Date.now() - started });
    }
  }

  for (const item of runs) {
    process.stdout.write(
      `${item.run} ${item.mode.padEnd(7)} tokens=${item.tokens} cost=$${item.cost.toFixed(5)} ` +
      `duration=${item.durationMs}ms tools=${item.tools.length}\n`
    );
  }

  for (const mode of ["with", "without"]) {
    const selected = runs.filter((item) => item.mode === mode);
    process.stdout.write(
      `${mode}: median tokens=${median(selected.map((item) => item.tokens))}, ` +
      `median cost=$${median(selected.map((item) => item.cost)).toFixed(5)}, ` +
      `median duration=${median(selected.map((item) => item.durationMs))}ms\n`
    );
  }
}

main().catch((error) => {
  process.stderr.write(`opencode-ab: ${error.message}\n`);
  process.exit(1);
});
