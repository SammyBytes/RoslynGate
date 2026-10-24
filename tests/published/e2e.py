#!/usr/bin/env python3
"""End-to-end check against a published RoslynGate binary or npm wrapper."""

from __future__ import annotations

import argparse
import json
import select
import shlex
import subprocess
import sys
from pathlib import Path


class McpSession:
    def __init__(self, command: list[str], solution: Path, cwd: Path):
        self.process = subprocess.Popen(
            [*command, "--solution", str(solution), "--no-prewarm"],
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
            raise TimeoutError("Timed out waiting for the published server.")
        line = self.process.stdout.readline()
        if not line:
            raise RuntimeError("The published server closed stdout.")
        return line

    def request(self, method: str, params: dict | None = None) -> dict:
        self.next_id += 1
        message = {"jsonrpc": "2.0", "id": self.next_id, "method": method}
        if params is not None:
            message["params"] = params
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()
        return json.loads(self.read_line())

    def notify(self, method: str) -> None:
        self.process.stdin.write(json.dumps({"jsonrpc": "2.0", "method": method}) + "\n")
        self.process.stdin.flush()

    def initialize(self) -> None:
        self.request(
            "initialize",
            {
                "protocolVersion": "2025-06-18",
                "capabilities": {},
                "clientInfo": {"name": "published-e2e", "version": "0"},
            },
        )
        self.notify("notifications/initialized")

    def call_tool(self, name: str, arguments: dict) -> dict:
        response = self.request("tools/call", {"name": name, "arguments": arguments})
        if response.get("result", {}).get("isError", False):
            raise AssertionError(f"{name} returned isError=true: {response}")
        text = response["result"]["content"][0]["text"]
        return json.loads(text)

    def close(self) -> None:
        try:
            self.process.stdin.close()
            self.process.wait(timeout=10)
        except Exception:
            self.process.kill()
            self.process.wait()


def line_of(path: Path, text: str) -> int:
    for index, line in enumerate(path.read_text().splitlines(), start=1):
        if text in line:
            return index
    raise AssertionError(f"Could not find '{text}' in {path}.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--command", required=True, help="Binary path or wrapper command.")
    parser.add_argument("--solution", required=True, type=Path)
    parser.add_argument("--repo", default=Path("."), type=Path)
    parser.add_argument("--version", default="0.2.2")
    args = parser.parse_args()

    command = shlex.split(args.command)
    solution = args.solution.resolve()
    repo = args.repo.resolve()
    fixture = solution.parent

    version = subprocess.run(
        [*command, "--version"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=True,
    ).stdout.strip()
    if version != args.version:
        raise AssertionError(f"Expected version {args.version}, got {version}.")

    session = McpSession(command, solution, repo)
    try:
        session.initialize()

        tools = session.request("tools/list")["result"]["tools"]
        names = {tool["name"] for tool in tools}
        expected = {
            "get_symbol_definition",
            "find_symbol_references",
            "get_type_hierarchy",
            "search_symbols",
            "reload_workspace",
        }
        if names != expected:
            raise AssertionError(f"Unexpected tools: {sorted(names)}")

        definition = session.call_tool(
            "get_symbol_definition",
            {"name": "SampleSdk.Widget.Echo"},
        )
        if definition.get("n") != "Echo" or definition.get("q") != "SampleSdk.Widget.Echo":
            raise AssertionError(f"Unexpected definition: {definition}")

        widget = fixture / "SampleSdk" / "Widget.cs"
        references = session.call_tool(
            "find_symbol_references",
            {
                "file": "SampleSdk/Widget.cs",
                "line": line_of(widget, "public Task Open(string doorId)"),
                "limit": 10,
            },
        )
        if references.get("total", 0) < 2 or not references.get("refs"):
            raise AssertionError(f"Unexpected references: {references}")

        hierarchy = session.call_tool("get_type_hierarchy", {"name": "SampleSdk.Widget"})
        if "SampleSdk.DeviceBase" not in hierarchy.get("b", []):
            raise AssertionError(f"Unexpected hierarchy: {hierarchy}")

        search = session.call_tool("search_symbols", {"query": "Widget", "limit": 10})
        if search.get("total", 0) == 0 or not search.get("hits"):
            raise AssertionError(f"Unexpected search: {search}")

        reloaded = session.call_tool("reload_workspace", {})
        if reloaded.get("status") != "reloaded" or reloaded.get("projects", 0) < 1:
            raise AssertionError(f"Unexpected reload: {reloaded}")
    finally:
        session.close()

    print(f"published E2E ok: RoslynGate {version}, 5 tools")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"published E2E failed: {error}", file=sys.stderr)
        raise SystemExit(1)
