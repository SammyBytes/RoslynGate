const fs = require("node:fs");

function stripJsonc(text) {
  let result = "";
  let inString = false;
  let escaped = false;
  let lineComment = false;
  let blockComment = false;

  for (let index = 0; index < text.length; index += 1) {
    const current = text[index];
    const next = text[index + 1];

    if (lineComment) {
      if (current === "\n") {
        lineComment = false;
        result += current;
      }
      continue;
    }

    if (blockComment) {
      if (current === "*" && next === "/") {
        blockComment = false;
        index += 1;
      }
      continue;
    }

    if (inString) {
      result += current;
      if (escaped) {
        escaped = false;
      } else if (current === "\\") {
        escaped = true;
      } else if (current === "\"") {
        inString = false;
      }
      continue;
    }

    if (current === "\"") {
      inString = true;
      result += current;
      continue;
    }

    if (current === "/" && next === "/") {
      lineComment = true;
      index += 1;
      continue;
    }

    if (current === "/" && next === "*") {
      blockComment = true;
      index += 1;
      continue;
    }

    result += current;
  }

  return result.replace(/,\s*([}\]])/g, "$1");
}

function readConfig(file) {
  if (!fs.existsSync(file)) {
    return { mcp: { servers: {} } };
  }

  return JSON.parse(stripJsonc(fs.readFileSync(file, "utf8")));
}

module.exports = { readConfig, stripJsonc };
