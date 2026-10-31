import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

export const PACKAGE = "roslyngate-ts";
export const REGISTRY = "https://registry.npmjs.org";
const SERVER = "roslyngate-ts";

export type UpgradeOptions = {
  version: string;
  cwd?: string;
  env?: NodeJS.ProcessEnv;
  platform?: NodeJS.Platform;
  scriptPath?: string;
  packageManager?: "npm" | "bun";
  cliPath?: string;
  fetchImpl?: typeof fetch;
  spawnImpl?: typeof spawn;
  installImpl?: (version: string, options: Record<string, unknown>) => Promise<void>;
};

export type UpgradeArgs = {
  check: boolean;
  dryRun: boolean;
  noConfig: boolean;
  manager?: "npm" | "bun";
  help: boolean;
};

export function parseUpgradeArgs(args: string[]): UpgradeArgs {
  const parsed: UpgradeArgs = {
    check: false,
    dryRun: false,
    noConfig: false,
    help: false
  };

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (argument === "--check") parsed.check = true;
    else if (argument === "--dry-run") parsed.dryRun = true;
    else if (argument === "--no-config") parsed.noConfig = true;
    else if (argument === "--help" || argument === "-h") parsed.help = true;
    else if (argument === "--manager") {
      const value = args[++index];
      if (value !== "npm" && value !== "bun") {
        throw new Error("--manager must be npm or bun.");
      }
      parsed.manager = value;
    } else {
      throw new Error(`Unknown upgrade option: ${argument}`);
    }
  }

  return parsed;
}

export function detectPackageManager({
  env = process.env,
  scriptPath = process.argv[1]
}: { env?: NodeJS.ProcessEnv; scriptPath?: string } = {}): "npm" | "bun" {
  const normalized = (scriptPath ?? "").replace(/\\/g, "/").toLowerCase();
  const bunRoot = (env.BUN_INSTALL ?? "").replace(/\\/g, "/").toLowerCase();
  if (bunRoot && normalized.startsWith(`${bunRoot}/`)) return "bun";
  return normalized.includes("/bun/install/global/") ? "bun" : "npm";
}

export function isNewer(candidate: string, current: string): boolean {
  const left = candidate.split("-")[0].split(".").map(Number);
  const right = current.split("-")[0].split(".").map(Number);
  for (let index = 0; index < Math.max(left.length, right.length); index += 1) {
    const difference = (left[index] ?? 0) - (right[index] ?? 0);
    if (difference !== 0) return difference > 0;
  }
  return false;
}

export async function fetchLatestVersion({
  fetchImpl = globalThis.fetch,
  registry = REGISTRY,
  packageName = PACKAGE
}: {
  fetchImpl?: typeof fetch;
  registry?: string;
  packageName?: string;
} = {}): Promise<string> {
  const response = await fetchImpl(`${registry}/${packageName}/latest`, {
    redirect: "follow",
    headers: { accept: "application/json" }
  });
  if (!response.ok) {
    throw new Error(`Could not query npm (${response.status}).`);
  }
  const data = (await response.json()) as { version?: unknown };
  if (!data || typeof data.version !== "string") {
    throw new Error("npm response has no version.");
  }
  return data.version;
}

export function installLatest(
  version: string,
  {
    manager = "npm",
    platform = process.platform,
    spawnImpl = spawn,
    env = process.env
  }: {
    manager?: "npm" | "bun";
    platform?: NodeJS.Platform;
    spawnImpl?: typeof spawn;
    env?: NodeJS.ProcessEnv;
  } = {}
): Promise<void> {
  if (!/^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/.test(version)) {
    throw new Error(`Invalid package version: ${version}`);
  }

  let command: string;
  let args: string[];
  if (manager === "bun") {
    command = platform === "win32" ? "bun.exe" : "bun";
    args = ["install", "-g", `${PACKAGE}@${version}`];
  } else if (platform === "win32") {
    command = env.ComSpec ?? "cmd.exe";
    args = ["/d", "/s", "/c", `npm install -g ${PACKAGE}@${version}`];
  } else {
    command = "npm";
    args = ["install", "-g", `${PACKAGE}@${version}`];
  }

  return new Promise((resolve, reject) => {
    const child = spawnImpl(command, args, { stdio: "inherit" });
    child.on("error", reject);
    child.on("exit", (code, signal) => {
      if (signal) reject(new Error(`package install stopped by ${signal}.`));
      else if (code !== 0) reject(new Error(`package install exited with code ${code}.`));
      else resolve();
    });
  });
}

export async function runUpgrade(args: string[], options: UpgradeOptions): Promise<void> {
  const {
    version,
    cwd = process.cwd(),
    env = process.env,
    platform = process.platform,
    scriptPath,
    packageManager,
    cliPath = path.resolve(process.argv[1] ?? ""),
    fetchImpl,
    spawnImpl,
    installImpl = installLatest
  } = options;

  const parsed = parseUpgradeArgs(args);
  if (parsed.help) {
    process.stdout.write(
      "Usage: roslyngate-ts upgrade [--check] [--dry-run] [--no-config] [--manager npm|bun]\n" +
        "Updates the global package and refreshes managed MCP entries.\n"
    );
    return;
  }

  const latest = await fetchLatestVersion({
    fetchImpl,
    registry: env.ROSLYGATE_REGISTRY
  });

  if (parsed.check) {
    process.stdout.write(`roslyngate-ts ${version}; latest ${latest}.\n`);
    return;
  }

  const manager =
    parsed.manager ?? packageManager ?? detectPackageManager({ env, scriptPath });
  const newer = isNewer(latest, version);

  if (newer) {
    if (parsed.dryRun) {
      process.stdout.write(`Would run ${manager} install -g ${PACKAGE}@${latest}.\n`);
    } else {
      process.stdout.write(`roslyngate-ts: updating ${version} -> ${latest} with ${manager}...\n`);
      await installImpl(latest, { manager, platform, spawnImpl, env });
    }
  } else {
    process.stdout.write(`roslyngate-ts: ${version} is up to date.\n`);
  }

  const target = newer ? latest : version;
  if (parsed.noConfig) {
    process.stdout.write("roslyngate-ts: skipped MCP config because --no-config is set.\n");
  } else {
    const updated = refreshConfigs(cwd, {
      dryRun: parsed.dryRun,
      cliPath
    });
    for (const file of updated) {
      process.stdout.write(
        `${parsed.dryRun ? "Would update" : "roslyngate-ts: updated"} ${file}\n`
      );
    }
  }

  process.stdout.write(
    `roslyngate-ts: ${target} is ready. Restart your MCP client to reload the server.\n`
  );
}

function refreshConfigs(
  cwd: string,
  { dryRun, cliPath }: { dryRun: boolean; cliPath: string }
): string[] {
  const updated: string[] = [];
  for (const name of [".mcp.json", "opencode.json", "opencode.jsonc"]) {
    const file = path.join(cwd, name);
    if (!fs.existsSync(file)) continue;
    let config: Record<string, any>;
    try {
      config = JSON.parse(fs.readFileSync(file, "utf8"));
    } catch {
      continue;
    }
    if (!refreshEntry(config, cliPath)) continue;
    updated.push(file);
    if (!dryRun) {
      fs.writeFileSync(file, `${JSON.stringify(config, null, 2)}\n`);
    }
  }
  return updated;
}

function refreshEntry(config: Record<string, any>, cliPath: string): boolean {
  const entries = [
    config?.mcpServers?.[SERVER],
    config?.mcp?.servers?.[SERVER]
  ];
  let changed = false;
  for (const entry of entries) {
    if (!entry || typeof entry !== "object") continue;
    const args = Array.isArray(entry.args) ? entry.args : undefined;
    if (args) {
      if (replaceCli(args, cliPath)) changed = true;
    }
    if (Array.isArray(entry.command)) {
      if (replaceCli(entry.command, cliPath)) changed = true;
    }
  }
  return changed;
}

function replaceCli(args: unknown[], cliPath: string): boolean {
  for (let index = 0; index < args.length; index += 1) {
    const value = args[index];
    if (typeof value !== "string") continue;
    if (value === cliPath) return false;
    if (/roslyngate-ts[/\\]dist[/\\]cli\.js$/.test(value)) {
      args[index] = cliPath;
      return true;
    }
  }
  return false;
}
