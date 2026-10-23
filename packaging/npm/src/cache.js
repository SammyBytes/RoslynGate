const os = require("node:os");
const path = require("node:path");

function cacheRoot(env = process.env, platform = process.platform) {
  if (env.ROSLYGATE_CACHE_DIR) {
    return env.ROSLYGATE_CACHE_DIR;
  }

  if (platform === "win32") {
    const local = env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(local, "roslyngate");
  }

  const base = env.XDG_CACHE_HOME || path.join(os.homedir(), ".cache");
  return path.join(base, "roslyngate");
}

function binaryPath({ version, asset, env, platform }) {
  return path.join(cacheRoot(env, platform), version, asset);
}

module.exports = { cacheRoot, binaryPath };
