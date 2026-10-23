# MCP clients

The recommended command is the npm wrapper, which downloads and caches the
self-contained binary on first use:

```bash
npm install -g roslyngate
roslyngate --solution /absolute/path/to/repository
```

You can also use `npx -y roslyngate` or `bunx roslyngate` without installing.

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
      "command": "roslyngate",
      "args": ["--solution", "/absolute/path/to/repository"]
    }
  }
}
```

## Claude Code

```bash
claude mcp add roslyngate -- roslyngate --solution /absolute/path/to/repository
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
      "command": "roslyngate",
      "args": ["--solution", "/absolute/path/to/repository"]
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

For an installed version, use `roslyngate` on PATH. If you use `npx` after npm publication, replace the command with `npx` and prefix the arguments with `-y roslyngate`.

```jsonc
{
  "mcp": {
    "servers": {
      "roslyngate": {
        "type": "local",
        "command": ["roslyngate", "--solution", "/absolute/path/to/repository"],
        "codemode": false
      }
    }
  }
}
```

Run `/mcps` in OpenCode to confirm the connection. Set `codemode` to `true` or
remove it to expose the tools through Code Mode.
