import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { ProjectFailure, detectTypeScriptProject } from "../project.js";

function temporary(): string {
  return fs.mkdtempSync(path.join(os.tmpdir(), "roslyngate-ts-project-"));
}

test("detects a tsconfig and a jsconfig project", () => {
  const root = temporary();
  try {
    fs.writeFileSync(path.join(root, "tsconfig.json"), "{}");
    const selection = detectTypeScriptProject(root);
    assert.equal(selection.root, root);
    assert.equal(selection.configPath, path.join(root, "tsconfig.json"));

    const file = detectTypeScriptProject(path.join(root, "tsconfig.json"));
    assert.equal(file.root, root);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("points to the .NET package when only a solution exists", () => {
  const root = temporary();
  try {
    fs.writeFileSync(path.join(root, "Sample.sln"), "");
    assert.throws(
      () => detectTypeScriptProject(root),
      (error: unknown) => {
        assert.ok(error instanceof ProjectFailure);
        assert.equal(error.code, "wrong_project");
        assert.match(error.message, /roslyngate-ts/);
        assert.match(error.hint, /roslyngate /);
        return true;
      }
    );
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("explains the missing project and explicit flags", () => {
  const root = temporary();
  try {
    assert.throws(
      () => detectTypeScriptProject(root),
      (error: unknown) => error instanceof ProjectFailure && error.code === "no_project"
    );
    fs.writeFileSync(path.join(root, "tsconfig.json"), "{}");
    assert.throws(
      () => detectTypeScriptProject(root, process.cwd(), "dotnet"),
      (error: unknown) => error instanceof ProjectFailure && error.code === "wrong_project"
    );
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
