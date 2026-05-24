# PiLoop Architecture

## Positioning

PiLoop is a **Pi-native orchestration runtime and workflow package**.

It is not a universal orchestrator for every agent harness.

## What is stable vs harness-specific

### Stable concepts
These should remain conceptually stable across projects:
- planning loop
- build/execution loop
- sprint/task issue model
- breadcrumb audit protocol
- worker result contract
- deterministic gates and retries
- project-local prompts and skills

### Pi-specific runtime layer
These are intentionally Pi-specific and should stay that way:
- Pi RPC transport
- Pi process lifecycle management
- Pi event parsing
- Pi transport failure classification
- Pi retry policy for websocket/provider transport failures
- Pi prompt/skill loading expectations

## Layers

### 1. Workflow layer
Defines:
- phases
- transitions
- retry rules
- escalation policy
- issue update rules

### 2. Contract layer
Defines:
- worker input contract
- worker structured output contract
- artifact declarations
- breadcrumb schema

### 3. Pi runtime layer
Implements:
- process host
- RPC runner
- event parsing
- timeout handling
- retry on transient transport failures
- failure classification: transport, auth, rate limit, quota, timeout, process, rpc, unknown

### 4. Project integration layer
Adapts PiLoop to a target repo by:
- reading `TEAM-ORCHESTRATION.md`
- creating or updating `.pi/prompts/`
- creating or updating `.agents/skills/`
- writing sprint artifacts under the target repo's `docs/`
- using the target repo as the unit of GitHub issue creation and local execution

## Repository strategy

PiLoop should live in this orchestration repo as its own project, while target application repos remain consumers.

That means PiLoop needs:
- its own source code
- its own docs
- examples of installation into target repos
- templates or generators for project-local assets

## Non-goals

- one runtime for any harness
- lowest-common-denominator abstraction over incompatible agent systems
- letting workers control orchestration flow
- durable local log history
- direct GitHub writes from workers
