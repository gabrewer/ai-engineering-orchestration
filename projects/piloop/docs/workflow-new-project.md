# Workflow: Drop PiLoop into a New Project

## Goal

Bootstrap a brand-new software project with PiLoop from the start, so planning and execution are built around Pi-native orchestration from day one.

Before PiLoop planning starts, use Pi discovery skills such as `/inspire` and `/brainstorm` to create a durable PRD under `docs/`. See `docs/discovery-to-piloop.md`.

## Desired outcome

A new repo should end up with:
- a `TEAM-ORCHESTRATION.md`
- PiLoop-compatible prompts and skills
- a docs structure for PRDs and sprint plans
- a working planning command
- a GitHub issue workflow ready to act as the durable audit trail

## Workflow

### Step 1 — Create the target repository
Create the new application repo first.

Required minimum state:
- git initialized
- GitHub remote connected
- default branch established
- human owner decides the product/app purpose

### Step 2 — Add a project-level `TEAM-ORCHESTRATION.md`
Write a project-specific orchestration brief that describes:
- product/domain context
- architecture constraints
- coding standards
- testing expectations
- worker roster
- GitHub conventions
- what counts as done

This file becomes the source material for generating project-specific prompts later.

### Step 3 — Install PiLoop project assets
PiLoop should add or generate:
- `.pi/prompts/`
- `.agents/skills/`
- `docs/`
- `docs/sprints/`
- `.agentloop/` or successor runtime state directory

For new projects, PiLoop can install a fuller default set because there is no legacy to preserve.

### Step 4 — Install starter prompts and skills
New project bootstrap should provide:
- shared orchestration skills
- planning worker prompts
- build worker prompts
- review/destroyer prompts

These may start from PiLoop templates, then be specialized using the project's `TEAM-ORCHESTRATION.md`.

### Step 5 — Create the first PRD
Use Pi discovery skills such as `/inspire` and `/brainstorm`, then write the first PRD under `docs/`.

PiLoop does not invent the product vision by default; it expects the human or a planning workflow to establish it.

### Step 6 — Run planning mode
Run PiLoop planning against the PRD.

Planning mode should:
- create sprint briefs
- create sprint plan JSON artifacts
- raise questions if ambiguity blocks planning
- create or update GitHub epic/task issues

### Step 7 — Human review gate
A human reviews the planned sprint outputs before build-mode execution is enabled.

### Step 8 — Enable build mode
After plan approval, PiLoop can run execution/build loops against the new project.

## What PiLoop should automate for new projects

PiLoop should eventually automate most of this path:
- repo asset installation
- default folder creation
- starter prompt/skill generation
- sample PRD scaffolding
- GitHub label setup for non-final workflow labels only; PiLoop must not apply final disposition labels like `done`, `complete`, or `shipped`
- first planning-run command hints

## Design principle

For a new repo, PiLoop should be opinionated.

Because no legacy system exists yet, the new-project workflow should optimize for consistency, speed, and a complete working orchestration environment.
