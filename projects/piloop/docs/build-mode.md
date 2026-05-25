# PiLoop Build Mode

Build mode executes sprint plan tasks with Pi workers and publishes defensible implementation evidence.

## Commands

```bash
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint-name-or-json>
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --all
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint> --skip-github
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint> --no-commit
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint> --resume
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint> --no-worktree
```

## Branch safety

PiLoop build mode must not commit directly to `main` or `master`.

By default, when started from `main` or `master`, PiLoop creates/uses `feature/<sprint>`. If a protected branch is explicitly requested with `--branch main` or `--branch master`, build mode fails before executing workers.

PiLoop does not push branches automatically. Humans decide when to push and open a PR.

## Worktree isolation

By default, build mode creates a sibling worktree under `../wt/` and executes the sprint there. This keeps the target root clean while workers modify files and create task commits.

Example worktree path:

```text
../wt/piloop-<sprint>-<timestamp>
```

Use `--no-worktree` only when you intentionally want workers to modify the target root directly.

## Current behavior

Before task execution, PiLoop runs sprint-level phases when enabled by the sprint JSON:

- `domain-modeler` writes `docs/domain/<sprint>.md` when `phases.domainModeling` is true
- `api-developer` writes `docs/api/<sprint>.md` when `phases.apiContract` is true

For each task, PiLoop:

1. marks task execution as started in GitHub Issues when enabled
2. runs `test-writer` unless `skipTests` is true
3. runs `backend-builder`, `frontend-builder`, or both based on task type
4. runs validation as a build/test gate
5. runs `destroyer` for adversarial review when validation passes
6. runs `review-agent`
7. reruns builders up to 3 review attempts when `review-agent` returns `changes_needed`
8. captures changed files with `git status`
9. runs detected validation:
   - `dotnet build/test` when a root `.sln` exists
   - `npm test` when `package.json` exists
   - otherwise records validation as skipped
10. commits task changes unless `--no-commit` is specified and validation/review passed
11. publishes a task evidence comment with intent, plan, branch, commit SHA when committed, work performed, decisions, blockers, test results, remaining issues, artifacts, and summary

## Resume behavior

`--resume` loads `.piloop/state-<sprint>.json`, keeps the previous branch and issue map unless explicitly overridden, and skips tasks already marked `done`.

Tasks marked `failed`, `building`, `testing`, `destroying`, or `reviewing` are retried because they did not complete successfully.

## Current limitations

- This is the first Pi-native build extraction and intentionally simpler than the original TrakPomo loop.
- It does not yet run git-committer as a separate worker phase; commits are created by PiLoop directly.
- It executes tasks sequentially rather than dependency waves.
- It does not yet auto-fix failed validation.
- Worktree resume currently starts a fresh worktree; use `--no-worktree --resume` to resume existing state in the target root.

These limitations are expected to be closed in later extraction passes.
