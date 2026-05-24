# Discovery to PiLoop Workflow

PiLoop starts after product intent has been shaped into a PRD.

The intended upstream workflow is:

1. use Pi interactively for discovery
2. create a durable PRD in the target repo
3. run PiLoop planning from that PRD
4. run PiLoop build execution from the generated sprint plans

## 1. Discovery and ideation

Use Pi skills such as:

```text
/inspire
/brainstorm
```

The goal is to move from rough intent to a clear product direction.

Good discovery output should answer:

- what problem are we solving?
- who is this for?
- what outcome should the first version deliver?
- what is explicitly out of scope?
- what constraints or technology choices matter?
- what milestones make sense?

## 2. Create the PRD

Write the durable product requirement document into the target repo, for example:

```text
docs/PRD.md
docs/PRD-20260524-product-name.md
```

The PRD should contain:

- goal
- user/problem context
- milestones
- expected outcomes per milestone
- non-goals
- constraints and assumptions

PiLoop treats this PRD as the source input for planning.

## 3. Initialize PiLoop assets

```bash
dotnet run --project projects/piloop/src/PiLoop -- init --target-root <repo>
```

This installs target-repo orchestration assets:

- `.pi/prompts/`
- `.pi/skill-models.json`
- `.pi/extensions/skill-model-router.ts`
- `.agents/skills/`
- `docs/sprints/`

## 4. Plan from the PRD

```bash
dotnet run --project projects/piloop/src/PiLoop -- plan --target-root <repo> --prd docs/PRD.md
```

Planning produces:

- sprint briefs under `docs/sprints/`
- sprint JSON plans under `docs/sprints/`
- `docs/sprints/plan-manifest.json`
- GitHub epic/task issues unless `--skip-github` is used

## 5. Build from sprint plans

```bash
dotnet run --project projects/piloop/src/PiLoop -- build --target-root <repo> --prd <sprint-name-or-json>
```

Build mode executes sprint tasks and records implementation evidence:

- intent
- work performed
- decisions and rationale
- blockers
- test/validation results
- remaining issues
- artifacts/files changed

## Responsibility boundary

PiLoop does not replace early product discovery.

- `/inspire` and `/brainstorm` help create intent.
- The PRD captures intent durably.
- PiLoop turns the PRD into planned, auditable, executable work.
