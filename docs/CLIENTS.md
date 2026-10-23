# MCP clients

The recommended command is the npm wrapper, which downloads and caches the
self-contained binary on first use:

```bash
npx -y roslyngate --solution /absolute/path/to/repository
```

Replace `/absolute/path/to/repository` with the solution directory. `--solution`
is optional when the client starts the server with the repository as its
working directory.

For a locally built binary, run `dotnet build` and use the built DLL or published
binary. Never use `dotnet run` in a client configuration: its build output can
write to stdout and corrupt the MCP stream.

```text
/absolute/path/to/RoslynGate/src/RoslynGate.Cli/bin/Debug/net10.0/roslyngate.dll
```

## Generic stdio

Claude Desktop, Cursor, Cline, and other clients that use `mcpServers`:

```json
{
  "mcpServers": {
    "roslyngate": {
      "command": "npx",
      "args": [
        "-y",
        "roslyngate",
        "--solution",
        "/absolute/path/to/repository"
      ]
    }
  }
}
```

## Claude Code

```bash
claude mcp add roslyngate -- npx -y roslyngate --solution /absolute/path/to/repository
```

Or add the server to the project `.mcp.json` using the generic `mcpServers`
shape above.

## Cursor

Add the generic `mcpServers` block to `.cursor/mcp.json` in the project, or to
the global Cursor MCP configuration.

## VS Code

`.vscode/mcp.json` uses `servers` and an explicit `stdio` type:

```json
{
  "servers": {
    "roslyngate": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "-y",
        "roslyngate",
        "--solution",
        "/absolute/path/to/repository"
      ]
    }
  }
}
```

## OpenCode

The repository includes a development `opencode.jsonc` that runs the locally
built DLL:

```jsonc
{
  "mcp": {
    "servers": {
      "roslyngate": {
        "type": "local",
        "command": ["dotnet", "src/RoslynGate.Cli/bin/Debug/net10.0/roslyngate.dll"],
        "cwd": ".",
        "codemode": false
      }
    }
  }
}
```

For an installed version, use:

```jsonc
{
  "mcp": {
    "servers": {
      "roslyngate": {
        "type": "local",
        "command": ["npx", "-y", "roslyngate", "--solution", "/absolute/path/to/repository"],
        "codemode": false
      }
    }
  }
}
```

Run `/mcps` in OpenCode to confirm the connection. Set `codemode` to `true` or
remove it to expose the tools through Code Mode.
