# PiLoop Disposable Integration Test

PiLoop is tested against a disposable GitHub-backed target repo so planning can create real issues without polluting application repos.

## Target repo

- GitHub: `https://github.com/gabrewer/piloop-test`
- Local path: `X:/source/piloop-test`

## Baseline setup

```bash
dotnet run --project projects/piloop/src/PiLoop -- init --target-root X:/source/piloop-test
```

This installs default planning prompts under `.pi/prompts/`.

## Full planning run with GitHub issues

```bash
dotnet run --project projects/piloop/src/PiLoop -- plan --target-root X:/source/piloop-test --prd docs/PRD.md
```

Expected output:

- sprint briefs and sprint JSON files under `docs/sprints/`
- temporary runtime logs under `.piloop/`
- GitHub epic/task issues in `gabrewer/piloop-test`

## Local-only planning run

```bash
dotnet run --project projects/piloop/src/PiLoop -- plan --target-root X:/source/piloop-test --prd docs/PRD.md --skip-github
```

Expected output:

- local sprint artifacts under `docs/sprints/`
- temporary runtime logs under `.piloop/`
- no GitHub issue creation or updates

## Git hygiene

Commit durable planning artifacts from `docs/sprints/` when useful.
Do not commit `.piloop/`; it is temporary runtime state/log output.
