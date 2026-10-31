#!/usr/bin/env python3
"""Compare the .NET and TypeScript RoslynGate servers on the shared contract."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tests" / "published"))
from e2e import McpSession  # noqa: E402

TOOLS = {
    "get_symbol_definition",
    "find_symbol_references",
    "get_type_hierarchy",
    "search_symbols",
    "reload_workspace",
}

DOTNET = [
    "dotnet",
    str(ROOT / "src/RoslynGate.Cli/bin/Debug/net10.0/roslyngate.dll"),
]
TYPESCRIPT = ["node", str(ROOT / "products/typescript/dist/cli.js")]

DOTNET_SOLUTION = ROOT / "fixtures" / "SampleSolution"
TS_SOLUTION = ROOT / "fixtures" / "TsSample"


def shape(value):
    if isinstance(value, dict):
        return {key: shape(item) for key, item in sorted(value.items())}
    if isinstance(value, list):
        return {"list": [shape(item) for item in value[:1]]}
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "number"
    if value is None:
        return "null"
    return "string"


def call_error(session: McpSession, name: str, arguments: dict) -> dict:
    response = session.request("tools/call", {"name": name, "arguments": arguments})
    result = response["result"]
    assert result.get("isError") is True, response
    return json.loads(result["content"][0]["text"])


def check(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    if not (ROOT / "src/RoslynGate.Cli/bin/Debug/net10.0/roslyngate.dll").exists():
        raise SystemExit("Build the .NET solution before running contract parity.")
    if not (ROOT / "products/typescript/dist/cli.js").exists():
        raise SystemExit("Build the TypeScript product before running contract parity.")

    dotnet = McpSession(DOTNET, DOTNET_SOLUTION, ROOT)
    typescript = McpSession(TYPESCRIPT, TS_SOLUTION, ROOT)
    try:
        dotnet.initialize()
        typescript.initialize()

        dotnet_tools = dotnet.request("tools/list")["result"]["tools"]
        ts_tools = typescript.request("tools/list")["result"]["tools"]
        check(
            {tool["name"] for tool in dotnet_tools} == TOOLS,
            "The .NET server tools changed.",
        )
        check(dotnet_tools == ts_tools, "tools/list differs between .NET and TypeScript.")

        for session, name in ((dotnet, "Widget"), (typescript, "Widget")):
            found = session.call_tool("get_symbol_definition", {"name": name})
            check(found.get("n") == "Widget", found)
            check(found.get("k") == "class", found)
            check(not str(found.get("q", "")).startswith("/"), found)
            check({"n", "q", "k", "s", "f", "l"} <= set(found), found)

        for session, name in ((dotnet, "Open"), (typescript, "open")):
            ambiguous = session.call_tool("get_symbol_definition", {"name": name})
            check(ambiguous.get("ambiguous") is True, ambiguous)
            check(len(ambiguous.get("candidates", [])) > 1, ambiguous)
            check(len(ambiguous["candidates"][0]) == 5, ambiguous)

        overload = typescript.call_tool(
            "get_symbol_definition",
            {"name": "open", "parameterTypes": ["number"]},
        )
        check(overload.get("n") == "open", overload)
        check(overload.get("k") == "method", overload)

        for session, name in ((dotnet, "Widget"), (typescript, "Widget")):
            page = session.call_tool(
                "find_symbol_references", {"name": name, "limit": 1}
            )
            check(page.get("truncated") is True, page)
            check(page.get("nextOffset") == 1, page)
            check("offset=1" in page.get("hint", ""), page)
            check(len(page.get("refs", [])[0]) == 3, page)

            verbose = session.call_tool(
                "find_symbol_references", {"name": name, "detail": "Verbose", "limit": 1}
            )
            check(verbose.get("ambiguous") is False, verbose)
            check(verbose.get("limit") == 1, verbose)
            check("references" in verbose, verbose)

            definition = session.call_tool(
                "find_symbol_references",
                {"name": name, "includeDefinition": True},
            )
            check(definition.get("definition", {}).get("n") == name, definition)

            hierarchy = session.call_tool("get_type_hierarchy", {"name": name})
            check(hierarchy.get("t"), hierarchy)
            check(set(hierarchy) <= {"t", "b", "i", "d", "m", "truncated"}, hierarchy)

            search = session.call_tool("search_symbols", {"query": name, "limit": 1})
            check(search.get("truncated") is True, search)
            check(search.get("nextOffset") == 1, search)
            check("offset=1" in search.get("hint", ""), search)
            check(len(search.get("hits", [])[0]) == 4, search)

            missing = session.call_tool("get_symbol_definition", {"name": "NoSuchSymbol"})
            check(missing.get("symbol") is None, missing)
            check(missing.get("try", {}).get("tool") == "search_symbols", missing)

            reload = session.call_tool("reload_workspace", {})
            check(reload.get("status") == "reloaded", reload)
            check(reload.get("watch") in {"on", "off"}, reload)

        for session in (dotnet, typescript):
            short = call_error(session, "search_symbols", {"query": "W"})
            check(short["error"]["code"] == "query_too_short", short)
            check(
                short["error"]["hint"] == "Use a fragment such as Widget, Open, or Door.",
                short,
            )

            missing_target = call_error(session, "get_symbol_definition", {})
            check(missing_target["error"]["code"] == "invalid_target", missing_target)
            check(
                missing_target["error"]["hint"] == "Pass name, or file with a 1-based line.",
                missing_target,
            )

            not_indexed = call_error(
                session,
                "get_symbol_definition",
                {"file": "does-not-exist.ts", "line": 1},
            )
            check(not_indexed["error"]["code"] == "file_not_indexed", not_indexed)

        print("contract parity ok: tools/list, compact/verbose, errors, paging")
        return 0
    finally:
        dotnet.close()
        typescript.close()


if __name__ == "__main__":
    raise SystemExit(main())
