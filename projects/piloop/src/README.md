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
```

`init` installs default generic planning prompts under `.pi/prompts/` if they are missing.

`plan` runs Product Designer and PM workers through Pi RPC from the target repo root, writes sprint artifacts under the target repo's `docs/sprints/`, stores temporary runtime logs under `.piloop/`, and creates GitHub issues unless `--skip-github` is specified.
