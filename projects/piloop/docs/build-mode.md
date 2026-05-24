# PiLoop Build Mode

Build mode executes sprint plan tasks with Pi workers and publishes defensible implementation evidence.

## Commands

```bash
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint-name-or-json>
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --all
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint> --skip-github
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint> --no-commit
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint> --resume
```

## Current behavior

For each task, PiLoop:

1. marks task execution as started in GitHub Issues when enabled
2. runs `test-writer` unless `skipTests` is true
3. runs `backend-builder`, `frontend-builder`, or both based on task type
4. captures changed files with `git status`
5. runs detected validation:
   - `dotnet build/test` when a root `.sln` exists
   - `npm test` when `package.json` exists
   - otherwise records validation as skipped
6. commits task changes unless `--no-commit` is specified
7. publishes a task evidence comment with intent, plan, work performed, decisions, blockers, test results, remaining issues, artifacts, and summary

## Resume behavior

`--resume` loads `.piloop/state-<sprint>.json`, keeps the previous branch and issue map unless explicitly overridden, and skips tasks already marked `done`.

Tasks marked `failed`, `building`, `testing`, `destroying`, or `reviewing` are retried because they did not complete successfully.

## Current limitations

- This is the first Pi-native build extraction and intentionally simpler than the original TrakPomo loop.
- It does not yet run domain-modeler, api-developer, destroyer, review-agent, or git-committer phases.
- It executes tasks sequentially rather than dependency waves.
- It does not yet auto-fix failed validation.
- It operates in the target root directly rather than creating an isolated worktree.

These limitations are expected to be closed in later extraction passes.
