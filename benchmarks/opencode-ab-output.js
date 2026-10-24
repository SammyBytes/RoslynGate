function parseOutput(stdout) {
  const result = { tokens: 0, cost: 0, tools: [], text: "" };
  for (const line of stdout.split("\n")) {
    if (!line.trim()) {
      continue;
    }

    let event;
    try {
      event = JSON.parse(line);
    } catch {
      continue;
    }

    if (event.type === "step_finish") {
      const usage = event.part.tokens || {};
      result.tokens += (usage.input || 0)
        + (usage.output || 0)
        + (usage.reasoning || 0)
        + (usage.cache?.read || 0)
        + (usage.cache?.write || 0);
      result.cost += event.part.cost || 0;
    } else if (event.type === "tool_use") {
      result.tools.push(event.part.tool);
    } else if (event.type === "text") {
      result.text = event.part.text || result.text;
    }
  }

  return result;
}

function median(values) {
  const sorted = [...values].sort((left, right) => left - right);
  if (sorted.length === 0) return 0;
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0
    ? (sorted[middle - 1] + sorted[middle]) / 2
    : sorted[middle];
}

module.exports = { median, parseOutput };
