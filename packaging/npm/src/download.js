const crypto = require("node:crypto");
const fs = require("node:fs");
const fsp = fs.promises;
const path = require("node:path");
const { parseSha256 } = require("./checksum");

const DEFAULT_BASE_URL = "https://github.com/SammyBytes/RoslynGate/releases/download";

async function ensureBinary({
  asset,
  version,
  target,
  baseUrl = DEFAULT_BASE_URL,
  fetchImpl = globalThis.fetch,
  platform = process.platform
}) {
  if (fs.existsSync(target)) {
    return target;
  }

  const binaryUrl = `${baseUrl}/v${version}/${asset}`;
  const checksumResponse = await fetchImpl(`${binaryUrl}.sha256`, { redirect: "follow" });
  if (!checksumResponse.ok) {
    throw new Error(`Could not download checksum (${checksumResponse.status}) from ${binaryUrl}.sha256.`);
  }

  const expected = parseSha256(await checksumResponse.text());
  const response = await fetchImpl(binaryUrl, { redirect: "follow" });
  if (!response.ok) {
    throw new Error(`Could not download ${asset} (${response.status}) from ${binaryUrl}.`);
  }

  const content = Buffer.from(await response.arrayBuffer());
  const actual = crypto.createHash("sha256").update(content).digest("hex");
  if (actual !== expected) {
    throw new Error(`Checksum mismatch for ${asset}. Expected ${expected}, got ${actual}.`);
  }

  await fsp.mkdir(path.dirname(target), { recursive: true });
  const temporary = `${target}.${process.pid}.tmp`;

  try {
    await fsp.writeFile(temporary, content);
    await fsp.rename(temporary, target);
    if (platform !== "win32") {
      await fsp.chmod(target, 0o755);
    }
  } finally {
    await fsp.rm(temporary, { force: true });
  }

  return target;
}

module.exports = { ensureBinary, DEFAULT_BASE_URL };
