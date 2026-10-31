import fs from "node:fs";
import path from "node:path";

export type ProjectSelection = {
  root: string;
  configPath: string;
};

export class ProjectFailure extends Error {
  constructor(
    readonly code: string,
    message: string,
    readonly hint: string
  ) {
    super(message);
  }
}

const CONFIG_NAMES = ["tsconfig.json", "jsconfig.json"];
const SOLUTION_PATTERN = /\.slnx?$/i;

export function detectTypeScriptProject(
  input?: string,
  cwd = process.cwd(),
  explicit?: "ts" | "dotnet"
): ProjectSelection {
  const absolute = path.resolve(cwd, input ?? ".");

  if (explicit === "dotnet") {
    throw wrongProject(absolute, "the --dotnet flag");
  }

  if (fs.existsSync(absolute) && fs.statSync(absolute).isFile()) {
    const name = path.basename(absolute);
    if (CONFIG_NAMES.includes(name.toLowerCase())) {
      return { root: path.dirname(absolute), configPath: absolute };
    }
    if (SOLUTION_PATTERN.test(name)) {
      throw wrongProject(absolute, name);
    }
    throw new ProjectFailure(
      "no_project",
      `Not a TypeScript project file: ${absolute}.`,
      "Pass a tsconfig.json or jsconfig.json file, or a directory that contains one."
    );
  }

  const directory = fs.existsSync(absolute) ? absolute : cwd;
  const config = findConfig(directory);
  if (config) {
    return { root: directory, configPath: config };
  }

  const solution = findSolution(directory);
  if (solution) {
    throw wrongProject(solution, path.basename(solution));
  }

  throw new ProjectFailure(
    "no_project",
    `No tsconfig.json or jsconfig.json found in ${directory}.`,
    "Run roslyngate-ts from a TypeScript project or pass --solution <path>. For .NET solutions use the roslyngate package."
  );
}

function findConfig(directory: string): string | undefined {
  for (const name of CONFIG_NAMES) {
    const candidate = path.join(directory, name);
    if (fs.existsSync(candidate)) return candidate;
  }
  return undefined;
}

function findSolution(directory: string): string | undefined {
  if (!fs.existsSync(directory)) return undefined;
  const match = fs
    .readdirSync(directory)
    .filter((name) => SOLUTION_PATTERN.test(name))
    .sort()[0];
  return match ? path.join(directory, match) : undefined;
}

function wrongProject(target: string, detail: string): ProjectFailure {
  return new ProjectFailure(
    "wrong_project",
    `This looks like a .NET workspace (${detail}). roslyngate-ts only serves TypeScript and JavaScript: ${target}.`,
    "Use the .NET package instead: npx -y roslyngate --solution <path>."
  );
}
