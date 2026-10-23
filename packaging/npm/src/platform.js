const SUPPORTED = {
  "linux-x64": "linux-x64",
  "win32-x64": "win-x64",
  "darwin-arm64": "osx-arm64"
};

function resolveRid(platform, arch) {
  const rid = SUPPORTED[`${platform}-${arch}`];
  if (!rid) {
    throw new Error(
      `Unsupported platform ${platform}/${arch}. Supported: ${Object.keys(SUPPORTED).join(", ")}.`
    );
  }

  return rid;
}

function assetName(rid) {
  return `roslyngate-${rid}${rid.startsWith("win-") ? ".exe" : ""}`;
}

module.exports = { resolveRid, assetName, supported: Object.keys(SUPPORTED) };
