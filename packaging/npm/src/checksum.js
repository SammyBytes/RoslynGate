function parseSha256(text) {
  const digest = String(text || "").trim().split(/\s+/)[0].toLowerCase();
  if (!/^[a-f0-9]{64}$/.test(digest)) {
    throw new Error("Invalid SHA-256 checksum file.");
  }

  return digest;
}

module.exports = { parseSha256 };
