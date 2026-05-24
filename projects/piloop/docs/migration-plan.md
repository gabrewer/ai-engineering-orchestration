# PiLoop Migration Plan

## Goal

Move the Pi-native orchestration work currently being developed in TrakPomo into this repo as a reusable PiLoop project.

## Why move it here

The TrakPomo repo was useful as an incubation environment, but it is the wrong long-term home because:
- it mixes product code with orchestration runtime code
- it still contains old Codex-era runner baggage
- static analysis is polluted by legacy files
- the orchestration runtime should evolve independently from any one application repo

## Migration rules

### Move first
Port the Pi-native pieces first:
- CLI entrypoint
- Pi runtime models
- Pi RPC runner
- Pi process host
- result validation
- worker registry
- planning loop
- GitHub audit service
- GitHub issue service
- temp log handling
- selective state/worktree helpers

### Leave behind
Do not bring old Codex-era runtime code unless it is explicitly revived:
- Codex CLI integration
- old agent runner flow
- old pipeline/orchestrator classes that are no longer architecture truth
- old agent prompt files that are being replaced by Pi project-local prompts

## Migration phases

### Phase 1 — Documentation and project framing
- establish the PiLoop name and docs
- define new-project and existing-project workflows
- define architecture boundaries

### Phase 2 — Planning runtime extraction
- move Pi-native planning/runtime code into PiLoop
- make paths configurable around a target project root
- preserve end-to-end planning mode first

### Phase 3 — Target project integration workflow
- support consuming a separate target repo
- install prompts/skills into that target repo
- read target `TEAM-ORCHESTRATION.md`
- write planning artifacts into the target repo
- create GitHub issues in the target repo

### Phase 4 — Execution mode migration
- port build loop pieces that still fit the Pi-native design
- delete assumptions that only worked in the original incubation repo

## First consumer

The TrakPomo repo should become the first real consumer/integration target for PiLoop after extraction.

That gives PiLoop:
- a real-world validation target
- a migration test bed
- a clear separation between orchestration runtime and application repo
