# Workflow: Drop PiLoop into an Existing Project

## Goal

Adopt PiLoop inside an existing codebase without pretending the project was designed for it from day one.

Before PiLoop planning starts, use Pi discovery skills such as `/inspire` and `/brainstorm` when a new initiative needs product shaping. The durable handoff into PiLoop is a PRD under `docs/`. See `docs/discovery-to-piloop.md`.

## Desired outcome

An existing repo should end up with:
- project-specific orchestration instructions
- only the minimum required PiLoop assets added
- planning mode working first
- gradual adoption before autonomous execution mode

## Adoption principle

Existing projects need a more conservative workflow than new projects.

PiLoop should assume:
- there is already architecture
- there are existing scripts, tests, and standards
- there may be existing CI/CD and branching strategies
- there may be legacy code and partial conventions

So PiLoop should adapt, not overwrite.

## Workflow

### Step 1 — Inspect the repo
Before installing assets, inspect:
- repo structure
- languages/frameworks
- current docs
- test framework(s)
- package/build conventions
- branching strategy
- existing AI-specific files

### Step 2 — Create or refine `TEAM-ORCHESTRATION.md`
If absent, PiLoop should help create one.
If present, PiLoop should treat it as the source of truth.

The file should capture:
- current architecture
- repo-specific constraints
- coding/testing rules
- worker behavior boundaries
- issue/audit expectations
- which phases are safe to automate now

### Step 3 — Install only required project-local assets
For an existing repo, PiLoop should add only what is needed:
- `AGENTS.md` if no repo-level Pi context file exists, or a small Pi/PiLoop section appended to the existing project instructions
- `.pi/prompts/` for human-facing slash commands such as `/pm-agent` and `/team-lead`
- `.agents/skills/` for Pi worker agents (`product-designer`, `pm`, `domain-modeler`, `api-developer`, `test-writer`, `backend-builder`, `frontend-builder`, `destroyer`, `review-agent`, `git-committer`)
- docs folders if missing
- runtime state/log ignore rules if needed

Use `.agents/skills/` for worker agents by default because Pi discovers it and the files remain portable to other Agent Skills-compatible tools. Use `.pi/skills/` only for Pi-specific skills.

Do not replace existing repo structure just to match a template.

The minimum useful install looks like:

```text
AGENTS.md
.pi/prompts/pm-agent.md
.pi/prompts/team-lead.md
.agents/skills/<agent-name>/SKILL.md
.agentloop/tmp/        # ignored
.agentloop/logs/       # ignored
.agentloop/state-*.json # ignored
```

Each `SKILL.md` must include Pi-compatible Agent Skills frontmatter:

```markdown
---
name: backend-builder
description: Builds backend code for one assigned task in this repository. Use when implementing server-side changes from a PiLoop task.
---
```

Validate discovery before using the agents for real work:

```bash
pi --no-extensions --tools read,grep,find,ls -p "List the available project skills and prompt templates. Do not edit files."
```

### Step 4 — Generate prompts from project context
Existing projects should not rely on generic prompts for long.

PiLoop should use the repo's orchestration instructions to generate or refine:
- planning prompts
- team-lead/build-loop prompts
- builder prompts
- review prompts
- scope-boundary prompts

Minimum Pi prompt set:

- `.pi/prompts/pm-agent.md` — reads the design/spec, audits relevant source and tests, performs the Contract Impact Check, avoids duplicate issues, and creates/updates the authoritative sprint issue/file.
- `.pi/prompts/team-lead.md` — reads the sprint issue/file and comments, loads worker skills by path for each phase, posts agent updates, runs destroyer/reviewer/tester as mandatory gates, auto-remediates blockers within thresholds, posts the final completion matrix, and prepares a Ready for Acceptance Verification checklist derived from the original source-of-truth.

Generated prompts should be specific enough to name repo standards, exact source areas, verification commands, temp-file locations, quality-gate headings, and escalation thresholds.

### Step 5 — Create or select the PRD
For a new initiative in an existing repo, use Pi discovery skills such as `/inspire` and `/brainstorm`, then write or refine the PRD under `docs/`.

For an already-planned initiative, select the existing PRD that PiLoop should plan from.

### Step 6 — Prove planning mode first
The first successful adoption target is planning mode.

Planning mode should prove:
- Pi can launch correctly in this repo
- prompts and skills resolve correctly
- sprint docs can be written safely
- GitHub issue creation works for this repo
- human review can occur from GitHub and docs alone

### Step 7 — Review operational friction
Before enabling build mode, evaluate:
- transport reliability
- test command quality
- build command quality
- project-specific failure patterns
- whether issue granularity is correct
- whether prompts are too generic or too broad

### Step 8 — Enable execution mode gradually
Execution mode should be enabled in phases:
1. planning only
2. test-writer + builders in limited scope
3. destroyer/review loop
4. commit and smoke-test flow
5. multi-sprint or unattended runs only after trust is earned

## What PiLoop should automate for existing projects

PiLoop should eventually automate:
- repo inspection checklist
- `TEAM-ORCHESTRATION.md` bootstrap/refinement
- prompt/skill generation from current repo context
- planning-mode installation verification
- GitHub label and issue model setup, excluding final disposition labels like `done`, `complete`, or `shipped` unless a human applies them outside PiLoop automation

## Design principle

For existing repos, PiLoop should be incremental and respectful.

The right outcome is not “make the repo look like PiLoop.”
The right outcome is “make PiLoop work inside the repo that already exists.”
