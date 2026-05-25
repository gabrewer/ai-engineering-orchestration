# PiLoop Source

This directory holds the PiLoop runtime and CLI source as the Pi-native implementation is extracted from TrakPomo.

## Current project

- `PiLoop/PiLoop.csproj` — .NET 10 CLI entry point
- `PiLoop/Program.cs` — commands and target project argument model
- `PiLoop/Services/PlanningLoop.cs` — Pi-backed planning runtime extracted from TrakPomo

## Current commands

```bash
dotnet run --project projects/piloop/src/PiLoop -- inspect --target-root <repo>
dotnet run --project projects/piloop/src/PiLoop -- init --target-root <repo>
dotnet run --project projects/piloop/src/PiLoop -- plan --target-root <repo> --prd docs/PRD.md
dotnet run --project projects/piloop/src/PiLoop -- plan --target-root <repo> --prd docs/PRD.md --skip-github
dotnet run --project projects/piloop/src/PiLoop -- plan --target-root <repo> --prd docs/PRD.md --allow-new-issues
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint-name-or-json>
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --all --skip-github
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint-name-or-json> --resume
```

`init` installs default generic planning prompts under `.pi/prompts/` if they are missing.

`plan` runs Product Designer and PM workers through Pi RPC from the target repo root, writes sprint artifacts under the target repo's `docs/sprints/`, stores temporary runtime logs under `.piloop/`, writes `docs/sprints/plan-manifest.json`, and creates GitHub issues unless `--skip-github` is specified.

When rerunning against an existing sprint epic, PiLoop reuses known issues and does not create issues for newly generated task IDs unless `--allow-new-issues` is specified.

`build` runs Pi workers for sprint tasks, captures changed files, runs simple detected validation (`dotnet` solution or `npm test`), commits each completed task by default, and publishes task-level evidence comments when GitHub is enabled. Build mode refuses to commit directly to `main` or `master`; use feature branches and PRs.

Model selection uses explicit CLI overrides first, then `.pi/skill-models.json`, then prompt frontmatter `model:`, then Pi defaults. The installed Pi extension uses the same `.pi/skill-models.json` for interactive slash-command routing.
