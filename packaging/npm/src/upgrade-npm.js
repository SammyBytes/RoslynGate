const { spawn } = require("node:child_process");

const PACKAGE = "roslyngate";
const REGISTRY = "https://registry.npmjs.org";

function detectPackageManager({ env = process.env, scriptPath = process.argv[1] } = {}) {
  const normalized = (scriptPath || "").replace(/\\/g, "/").toLowerCase();
  const bunRoot = (env.BUN_INSTALL || "").replace(/\\/g, "/").toLowerCase();

  if (bunRoot && normalized.startsWith(`${bunRoot}/`)) {
    return "bun";
  }

  return normalized.includes("/bun/install/global/") ? "bun" : "npm";
}

function isNewer(candidate, current) {
  const parse = (value) => value.split("-")[0].split(".").map(Number);
  const left = parse(candidate);
  const right = parse(current);

  for (let index = 0; index < Math.max(left.length, right.length); index += 1) {
    const difference = (left[index] || 0) - (right[index] || 0);
    if (difference !== 0) {
      return difference > 0;
    }
  }

  return false;
}

async function fetchLatestVersion({ fetchImpl = globalThis.fetch, registry = REGISTRY } = {}) {
  const response = await fetchImpl(`${registry}/${PACKAGE}/latest`, {
    redirect: "follow",
    headers: { accept: "application/json" }
  });

  if (!response.ok) {
    throw new Error(`Could not query npm (${response.status}).`);
  }

  const data = await response.json();
  if (!data || typeof data.version !== "string") {
    throw new Error("npm response has no version.");
  }

  return data.version;
}

function installLatest(
  version,
  { manager = "npm", platform = process.platform, spawnImpl = spawn, env = process.env } = {}
) {
  if (!/^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/.test(version)) {
    throw new Error(`Invalid package version: ${version}`);
  }

  let command;
  let args;

  if (manager === "bun") {
    command = platform === "win32" ? "bun.exe" : "bun";
    args = ["install", "-g", `${PACKAGE}@${version}`];
  } else if (platform === "win32") {
    command = env.ComSpec || "cmd.exe";
    args = ["/d", "/s", "/c", `npm install -g ${PACKAGE}@${version}`];
  } else {
    command = "npm";
    args = ["install", "-g", `${PACKAGE}@${version}`];
  }

  return new Promise((resolve, reject) => {
    const child = spawnImpl(command, args, { stdio: "inherit" });

    child.on("error", reject);
    child.on("exit", (code, signal) => {
      if (signal) {
        reject(new Error(`package install stopped by ${signal}.`));
        return;
      }

      if (code !== 0) {
        reject(new Error(`package install exited with code ${code}.`));
        return;
      }

      resolve();
    });
  });
}

module.exports = {
  PACKAGE,
  REGISTRY,
  detectPackageManager,
  fetchLatestVersion,
  installLatest,
  isNewer
};
