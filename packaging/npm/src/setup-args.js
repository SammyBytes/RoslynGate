function parseArgs(args) {
  let solution = ".";
  let print = false;
  let dryRun = false;
  let skill = false;

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];

    if (argument === "claude") {
      continue;
    }

    if (argument === "--print") {
      print = true;
      continue;
    }

    if (argument === "--dry-run") {
      dryRun = true;
      continue;
    }

    if (argument === "--skill") {
      skill = true;
      continue;
    }

    if (argument === "--solution") {
      if (index + 1 >= args.length) {
        throw new Error("Missing value for --solution.");
      }
      solution = args[++index];
      continue;
    }

    if (argument.startsWith("--solution=")) {
      solution = argument.slice("--solution=".length);
      continue;
    }

    throw new Error(`Unknown setup option: ${argument}`);
  }

  return { solution, print, dryRun, skill };
}

module.exports = { parseArgs };
