const { spawn } = require("node:child_process");

const PACKAGE = "roslyngate";
const REGISTRY = "https://registry.npmjs.org";

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

function installLatest(version, { platform = process.platform, spawnImpl = spawn } = {}) {
  const command = platform === "win32" ? "npm.cmd" : "npm";
  const useShell = platform === "win32";

  return new Promise((resolve, reject) => {
    const child = spawnImpl(command, ["install", "-g", `${PACKAGE}@${version}`], {
      stdio: "inherit",
      shell: useShell
    });

    child.on("error", reject);
    child.on("exit", (code, signal) => {
      if (signal) {
        reject(new Error(`npm install stopped by ${signal}.`));
        return;
      }

      if (code !== 0) {
        reject(new Error(`npm install exited with code ${code}.`));
        return;
      }

      resolve();
    });
  });
}

module.exports = {
  PACKAGE,
  REGISTRY,
  fetchLatestVersion,
  installLatest,
  isNewer
};
