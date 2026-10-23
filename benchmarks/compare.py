#!/usr/bin/env python3
"""Compare RoslynGate tool output and latency with ripgrep baselines."""

from __future__ import annotations

import argparse
import json
import os
import select
import statistics
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path


@dataclass
class GrepResult:
    chars: int
    elapsed_ms: float
    command: str


class McpSession:
    def __init__(self, binary: Path, solution: Path, cwd: Path):
        self.process = subprocess.Popen(
            [str(binary), "--solution", str(solution), "--no-prewarm"],
            cwd=cwd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
        )
        self.next_id = 0

    def read_line(self, timeout: float = 180.0) -> str:
        ready, _, _ = select.select([self.process.stdout], [], [], timeout)
        if not ready:
            raise TimeoutError("Timed out waiting for the MCP server.")
        line = self.process.stdout.readline()
        if not line:
            raise RuntimeError("The MCP server closed stdout.")
        return line

    def request(self, method: str, params: dict | None = None) -> dict:
        self.next_id += 1
        message = {
            "jsonrpc": "2.0",
            "id": self.next_id,
            "method": method,
        }
        if params is not None:
            message["params"] = params
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()
        return json.loads(self.read_line())

    def notify(self, method: str) -> None:
        self.process.stdin.write(
            json.dumps({"jsonrpc": "2.0", "method": method}) + "\n"
        )
        self.process.stdin.flush()

    def initialize(self) -> float:
        start = time.perf_counter()
        self.request(
            "initialize",
            {
                "protocolVersion": "2025-06-18",
                "capabilities": {},
                "clientInfo": {"name": "benchmark", "version": "0"},
            },
        )
        elapsed = (time.perf_counter() - start) * 1000
        self.notify("notifications/initialized")
        return elapsed

    def call_tool(self, name: str, arguments: dict) -> tuple[str, float]:
        start = time.perf_counter()
        response = self.request(
            "tools/call",
            {"name": name, "arguments": arguments},
        )
        elapsed = (time.perf_counter() - start) * 1000
        text = response["result"]["content"][0]["text"]
        return text, elapsed

    def close(self) -> None:
        try:
            self.process.stdin.close()
        except Exception:
            pass
        try:
            self.process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()


def grep(repo: Path, *arguments: str) -> GrepResult:
    command = [
        "rg",
        "-n",
        "--no-heading",
        "--color",
        "never",
        "--glob",
        "!bin/**",
        "--glob",
        "!obj/**",
        *arguments,
        ".",
    ]
    start = time.perf_counter()
    result = subprocess.run(
        command,
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    elapsed = (time.perf_counter() - start) * 1000
    return GrepResult(len(result.stdout), elapsed, " ".join(command))


def p95(values: list[float]) -> float:
    if len(values) == 1:
        return values[0]
    ordered = sorted(values)
    index = min(len(ordered) - 1, round(0.95 * (len(ordered) - 1)))
    return ordered[index]


def rss_mb(pid: int) -> float:
    try:
        status = Path(f"/proc/{pid}/status").read_text()
    except OSError:
        return 0.0
    for line in status.splitlines():
        if line.startswith("VmRSS:"):
            return int(line.split()[1]) / 1024
    return 0.0


def markdown_table(rows: list[dict]) -> str:
    lines = [
        "| Task | RoslynGate calls | RoslynGate chars | RoslynGate ~tokens | RoslynGate p50 | Grep calls | Grep chars | Grep ~tokens | Grep ms | Token reduction |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for row in rows:
        reduction = 1 - (row["roslyn_chars"] / row["grep_chars"]) if row["grep_chars"] else 0
        lines.append(
            "| {task} | {roslyn_calls} | {roslyn_chars} | {roslyn_tokens} | "
            "{roslyn_ms:.1f} ms | {grep_calls} | {grep_chars} | {grep_tokens} | "
            "{grep_ms:.1f} ms | {reduction:.0%} |".format(
                task=row["task"],
                roslyn_calls=row["roslyn_calls"],
                roslyn_chars=row["roslyn_chars"],
                roslyn_tokens=round(row["roslyn_chars"] / 4),
                roslyn_ms=row["roslyn_ms"],
                grep_calls=row["grep_calls"],
                grep_chars=row["grep_chars"],
                grep_tokens=round(row["grep_chars"] / 4),
                grep_ms=row["grep_ms"],
                reduction=reduction,
            )
        )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--binary", required=True, type=Path)
    parser.add_argument("--solution", required=True, type=Path)
    parser.add_argument("--repo", default=Path("."), type=Path)
    parser.add_argument("--iterations", type=int, default=5)
    args = parser.parse_args()

    binary = args.binary.resolve()
    solution = args.solution.resolve()
    repo = args.repo.resolve()

    if not binary.exists():
        print(f"Binary not found: {binary}", file=sys.stderr)
        return 1

    tasks = [
        {
            "task": "Definition: DefinitionException",
            "tool": "get_symbol_definition",
            "arguments": {"name": "RoslynGate.Engine.DefinitionException"},
            "grep": (r"class DefinitionException", "-C", "2"),
        },
        {
            "task": "References: DefinitionException",
            "tool": "find_symbol_references",
            "arguments": {
                "name": "RoslynGate.Engine.DefinitionException",
                "limit": 50,
            },
            "grep": (r"DefinitionException", "-C", "1"),
        },
        {
            "task": "Hierarchy: QueryException",
            "tool": "get_type_hierarchy",
            "arguments": {"name": "RoslynGate.Engine.QueryException"},
            "grep": (r"QueryException", "-C", "1"),
        },
        {
            "task": "Search: Query",
            "tool": "search_symbols",
            "arguments": {"query": "Query", "limit": 20},
            "grep": (r"Query",),
        },
    ]

    session = McpSession(binary, solution, repo)
    try:
        handshake = session.initialize()
        first_text, first_ms = session.call_tool(tasks[0]["tool"], tasks[0]["arguments"])
        rows = []
        warm_latencies: list[float] = []

        for index, task in enumerate(tasks):
            texts: list[str] = []
            latencies: list[float] = []

            if index == 0:
                texts.append(first_text)

            for _ in range(args.iterations):
                text, elapsed = session.call_tool(task["tool"], task["arguments"])
                texts.append(text)
                latencies.append(elapsed)

            summary = grep(repo, *task["grep"])
            warm_latencies.extend(latencies)
            rows.append(
                {
                    "task": task["task"],
                    "roslyn_calls": 1,
                    "roslyn_chars": len(texts[-1]),
                    "roslyn_ms": statistics.median(latencies),
                    "grep_calls": 1,
                    "grep_chars": summary.chars,
                    "grep_ms": summary.elapsed_ms,
                }
            )

        peak_rss = rss_mb(session.process.pid)
    finally:
        session.close()

    print("# RoslynGate benchmark\n")
    print("Generated by `benchmarks/compare.py`.\n")
    print("## Token volume\n")
    print(markdown_table(rows))
    print("\n`~tokens` is `chars / 4`, an approximation. Grep output is raw text;")
    print("RoslynGate output is compact JSON with resolved locations.\n")
    print("## Latency\n")
    print(f"- Handshake: **{handshake:.1f} ms**")
    print(f"- First tool call, including workspace load: **{first_ms:.1f} ms**")
    print(f"- Warm tool calls p50: **{statistics.median(warm_latencies):.1f} ms**")
    print(f"- Warm tool calls p95: **{p95(warm_latencies):.1f} ms**")
    print(f"- Peak RSS after benchmark: **{peak_rss:.0f} MB**")
    print("\nGrep commands:")
    for task in tasks:
        print(f"- `{task['task']}`: `{grep(repo, *task['grep']).command}`")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
