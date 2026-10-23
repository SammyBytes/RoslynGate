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

## Windows and Claude Code

The simplest path is:

```powershell
npm install -g roslyngate
cd C:\Dev\MyRepo
roslyngate setup claude
```

`setup claude` downloads and caches the native Windows binary, writes
`.mcp.json` in the repository, and uses the native binary path directly. Open
Claude Code, run `/mcp`, and approve `roslyngate`.

You can inspect the file before writing:

```powershell
roslyngate setup claude --dry-run
roslyngate setup claude --print
```

If you prefer a manual binary setup, download `roslyngate-win-x64.exe` and its
`.sha256` from [Releases](https://github.com/SammyBytes/RoslynGate/releases),
verify the hash, and place it at a stable path such as
`C:\Tools\roslyngate\roslyngate.exe`.

```powershell
Get-FileHash "C:\Tools\roslyngate\roslyngate.exe" -Algorithm SHA256
& "C:\Tools\roslyngate\roslyngate.exe" --check --solution "C:\Dev\MyRepo"
```

Register it for the current repository:

```powershell
cd C:\Dev\MyRepo
claude mcp add --scope project --transport stdio roslyngate -- "C:\Tools\roslyngate\roslyngate.exe" --solution "C:\Dev\MyRepo"
```

Or create `.mcp.json` at the repository root:

```json
{
  "mcpServers": {
    "roslyngate": {
      "command": "C:\\Tools\\roslyngate\\roslyngate.exe",
      "args": ["--solution", "C:\\Dev\\MyRepo"]
    }
  }
}
```

Start `claude`, approve the project server, and check:

```text
/mcp
claude mcp list
```

If you prefer the npm wrapper, Windows requires `cmd /c`:

```powershell
claude mcp add --scope user --transport stdio roslyngate -- cmd /c npx -y roslyngate@0.1.3 --solution "C:\Dev\MyRepo"
```

Test `npx` directly in PowerShell before using it from Claude Code. Use project
scope, not user scope, when the server is tied to one repository path.

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
