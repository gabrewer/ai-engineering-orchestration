# PiLoop

PiLoop is a Pi-native orchestration workflow for planning and executing software work with specialized agents, deterministic gates, and GitHub issues as the durable system of record.

## Why PiLoop exists

PiLoop is intentionally specific to Pi.

The reusable ideas in orchestration are real, but the runtime details are harness-specific:
- process lifecycle
- RPC/event stream shape
- prompt and skill loading
- session model
- failure modes
- provider transport behavior
- tool-calling conventions

PiLoop embraces that reality instead of pretending one runtime can cleanly fit every harness.

## Primary goals

PiLoop should support two onboarding paths:

1. **Drop PiLoop into a brand-new project**
   - initialize orchestration structure from day one
   - install prompts, skills, conventions, and commands before the app exists

2. **Drop PiLoop into an existing project**
   - inspect the current repo
   - add only the orchestration assets needed
   - preserve the project's existing architecture and workflows where possible

## Core operating model

- Pi subprocesses are workers, not the boss
- the orchestrator enforces workflow
- GitHub Issues are the durable audit trail
- local logs are temporary only
- prompts define worker identity
- skills define reusable rules and behavior constraints
- project-specific prompts should eventually be generated from each project's own `TEAM-ORCHESTRATION.md`

## Initial project shape

- `docs/architecture.md` — what PiLoop is and is not
- `docs/workflow-new-project.md` — bootstrap workflow for empty/new repos
- `docs/workflow-existing-project.md` — adoption workflow for existing repos
- `docs/migration-plan.md` — how Pi-native code moves out of TrakPomo into this repo
- `docs/integration-test-piloop-test.md` — disposable GitHub-backed integration test notes

## Current status

PiLoop is being extracted from the TrakPomo `tools/agentloop/` implementation.
The first focus is preserving and documenting the Pi-native planning/runtime model before broader execution-mode migration.
