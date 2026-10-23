const fs = require("node:fs");
const path = require("node:path");
const pkg = require("../package.json");

const repositoryRoot = path.resolve(__dirname, "../../..");
const props = fs.readFileSync(path.join(repositoryRoot, "Directory.Build.props"), "utf8");
const match = props.match(/<Version>([^<]+)<\/Version>/);

if (!match) {
  console.error("Could not read <Version> from Directory.Build.props.");
  process.exit(1);
}

if (pkg.version !== match[1]) {
  console.error(
    `Version mismatch: package.json ${pkg.version}, Directory.Build.props ${match[1]}.`
  );
  process.exit(1);
}

console.log(`Version is synchronized: ${pkg.version}`);
