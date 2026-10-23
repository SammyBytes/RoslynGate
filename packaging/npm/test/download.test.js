const crypto = require("node:crypto");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const assert = require("node:assert/strict");
const { ensureBinary } = require("../src/download");

function fakeFetch(content, digest) {
  return async (url) => {
    if (url.endsWith(".sha256")) {
      return {
        ok: true,
        status: 200,
        text: async () => `${digest}  roslyngate-linux-x64`
      };
    }

    return {
      ok: true,
      status: 200,
      arrayBuffer: async () => content
    };
  };
}

test("downloads and verifies a binary", async () => {
  const content = Buffer.from("fake-roslyngate-binary");
  const digest = crypto.createHash("sha256").update(content).digest("hex");
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-npm-"));
  const target = path.join(root, "0.1.0", "roslyngate-linux-x64");

  try {
    const result = await ensureBinary({
      asset: "roslyngate-linux-x64",
      version: "0.1.0",
      target,
      baseUrl: "https://example.test",
      fetchImpl: fakeFetch(content, digest)
    });

    assert.equal(result, target);
    assert.deepEqual(fs.readFileSync(target), content);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("rejects a mismatched checksum and leaves no binary", async () => {
  const content = Buffer.from("fake-roslyngate-binary");
  const wrongDigest = "0".repeat(64);
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-npm-"));
  const target = path.join(root, "0.1.0", "roslyngate-linux-x64");

  try {
    await assert.rejects(
      ensureBinary({
        asset: "roslyngate-linux-x64",
        version: "0.1.0",
        target,
        baseUrl: "https://example.test",
        fetchImpl: fakeFetch(content, wrongDigest)
      }),
      /Checksum mismatch/
    );

    assert.equal(fs.existsSync(target), false);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("reuses a cached binary", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-npm-"));
  const target = path.join(root, "0.1.0", "roslyngate-linux-x64");
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, "cached");

  try {
    const result = await ensureBinary({
      asset: "roslyngate-linux-x64",
      version: "0.1.0",
      target,
      baseUrl: "https://example.test",
      fetchImpl: async () => {
        throw new Error("download should not be called");
      }
    });

    assert.equal(result, target);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
