import assert from "node:assert/strict";
import test from "node:test";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { TsWorkspace } from "../workspace.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const fixture = path.resolve(here, "../../../../fixtures/TsSample");

test("resolves TS symbols, references, hierarchy, and search", () => {
  const workspace = new TsWorkspace(fixture);
  try {
    workspace.load();

    const widget = workspace.resolve("Widget");
    assert.equal(widget.length, 1);
    assert.equal(widget[0].name, "Widget");

    const references = workspace.references(widget[0], 0, 50);
    assert.ok(references.total >= 4);

    const hierarchy = workspace.hierarchy(widget[0]);
    assert.ok(hierarchy.interfaces.includes("Openable"));
    assert.ok(hierarchy.derived.includes("SecureWidget"));

    const search = workspace.search("Widget", 0, 20);
    assert.ok(search.total > 0);
    assert.ok(search.hits.some((hit) => hit.name === "Widget"));
  } finally {
    workspace.close();
  }
});
