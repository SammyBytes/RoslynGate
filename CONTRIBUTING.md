# Contributing

## Setup

```bash
dotnet --version        # 10.0.1xx
dotnet restore --locked-mode
dotnet build RoslynGate.sln -warnaserror
dotnet test RoslynGate.sln
```

## Workflow

`main` is always green. Every change lands through a short-lived branch and a
pull request.

```bash
git switch main
git pull --ff-only
git switch -c feat/symbol-definition

git add -A
git commit -m "feat(engine): resolve symbols by position"

git push -u origin HEAD
gh pr create --base main --title "feat(engine): resolve symbols by position"
```

Squash merge only. Delete the branch after merge.

## Commits and pull requests

- English, imperative, lowercase, no trailing period, max 72 characters.
- Format: `type(scope): summary`.
- Types: `feat`, `fix`, `perf`, `refactor`, `docs`, `test`, `build`, `ci`,
  `chore`, `revert`.
- Scopes: `engine`, `mcp`, `cli`, `packaging`, `ci`, `docs`, `deps`.
- A pull request is one logical change. Keep it under 400 lines when possible.
- The pull request title becomes the commit on `main`.

## Rules

- `stdout` carries MCP messages only. Use `stderr` for diagnostics.
- Never guess an ambiguous symbol. Return candidates.
- Add a test with every behavior change.
- Run `dotnet format` before opening the pull request.
- Keep files under 150 lines when possible, 200 hard maximum.
