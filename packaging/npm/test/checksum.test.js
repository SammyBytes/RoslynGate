const test = require("node:test");
const assert = require("node:assert/strict");
const { parseSha256 } = require("../src/checksum");

test("reads the digest from a sha256sum line", () => {
  const digest = "a".repeat(64);
  assert.equal(parseSha256(`${digest}  roslyngate-linux-x64`), digest);
});

test("rejects malformed checksum files", () => {
  assert.throws(() => parseSha256("not-a-digest"), /Invalid SHA-256/);
});
