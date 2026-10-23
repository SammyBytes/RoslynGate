# MCP clients

Build the CLI first:

```bash
dotnet build
```

Use the built DLL or a published binary. Never use `dotnet run` in a client
configuration: its build output can write to stdout and corrupt the MCP stream.

```text
/absolute/path/to/RoslynGate/src/RoslynGate.Cli/bin/Debug/net10.0/RoslynGate.Cli.dll
```

Replace `/absolute/path/to/repository` with the solution directory. `--solution`
is optional when the client starts the server with the repository as its
working directory.

## Generic stdio

Claude Desktop, Cursor, Cline, and other clients that use `mcpServers`:

```json
{
  "mcpServers": {
    "roslyngate": {
      "command": "dotnet",
      "args": [
        "/absolute/path/to/RoslynGate.Cli.dll",
        "--solution",
        "/absolute/path/to/repository"
      ]
    }
  }
}
```

## Claude Code

```bash
claude mcp add roslyngate -- dotnet /absolute/path/to/RoslynGate.Cli.dll --solution /absolute/path/to/repository
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
      "command": "dotnet",
      "args": [
        "/absolute/path/to/RoslynGate.Cli.dll",
        "--solution",
        "/absolute/path/to/repository"
      ]
    }
  }
}
```

## OpenCode

The repository includes `opencode.jsonc`:

```jsonc
{
  "mcp": {
    "servers": {
      "roslyngate": {
        "type": "local",
        "command": ["dotnet", "src/RoslynGate.Cli/bin/Debug/net10.0/RoslynGate.Cli.dll"],
        "cwd": ".",
        "codemode": false
      }
    }
  }
}
```

Run `/mcps` in OpenCode to confirm the connection. Set `codemode` to `true` or
remove it to expose the tools through Code Mode.
