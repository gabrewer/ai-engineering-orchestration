# Workflow: Set Up Orchestration in a New Project

Use this workflow when starting a brand-new application and there is no existing codebase or agent configuration to preserve.

## Outcome

The project should have:

- a clear product brief or PRD;
- project-specific agent-generation instructions;
- tailored front-door prompts and worker skills;
- a configured state backend;
- an approved first sprint; and
- a safe path from planning to execution.

## Workflow

### 1. Create the target project directory

Create an empty directory for the new project and choose its repository location. This directory is the installation target for the baseline orchestration assets and the application code.

### 2. Define the product

Before configuring agents, establish the product intent with the human owner. Use discovery and brainstorming as needed, then create the first PRD under `docs/`.

The PRD should describe, at minimum:

- the problem and target users;
- the desired behavior and initial scope;
- exclusions and open questions;
- intended runtime, deployment, and integration environments; and
- initial acceptance expectations.

The orchestration system should not invent the product vision by default.

### 3. Install the baseline orchestration assets

Copy the baseline orchestration assets from this repository into the target project. At minimum, copy:

- `instructions/AGENT-GENERATION.md`;
- `instructions/TEAM-ORCHESTRATION.md`;
- `instructions/agents/`;
- the applicable `instructions/TOOL-*.md` adapter; and
- the shared skills and supporting templates.

These provide templates and canonical contracts. Preserve shared behavior and tailor project-specific files through the generation process.

### 4. Create the repository

Create the application repository and establish its basic conventions:

- initialize Git;
- establish the default branch and remote;
- add the initial project structure;
- record the intended language, framework, package manager, and build/test commands; and
- add any project-level repository instructions.

At this point the repository may be a skeleton. Generate detailed implementation agents once the available project context is recorded.

### 5. Choose the AI tool and configure its adapter

Select the tool or tools the project will use, such as Claude Code, GitHub Copilot, Codex, Pi, or opencode. For each selected tool:

- read its `TOOL-*.md` adapter instructions;
- install or generate its native project files and entry points;
- configure the required model, delegation, permissions, and tool access;
- add the tool's always-loaded project instructions; and
- verify that `pm-agent` owns planning, `team-lead` owns execution, and a configured `pr-agent` is separately invoked for pull-request lifecycle and review.

If the selected tool has no adapter, add one before generating project agents. Tool adapters define native setup and delegation; shared worker behavior and orchestration remain defined by the canonical instructions.

### 6. Choose the state backend

During one-time repository setup, the project owner selects one durable state backend:

- `github-issues` for GitHub-backed planning and auditability; or
- `filesystem` for local or private planning artifacts.

Persist the choice in `AGENTS.md` (or the harness-equivalent repository context file) using:

```markdown
**State backend:** github-issues
```

or:

```markdown
**State backend:** filesystem
```

Later planning and execution runs resolve the backend from this persisted choice.

### 7. Define generation inputs

Create the project-level `AGENT-GENERATION.md`. A useful starting prompt is:

```text
Using the PRD and current repository, create AGENT-GENERATION.md for this
project.

Read these orchestration references first:
- @instructions/AGENT-GENERATION.md — what agent generation must accomplish
- @instructions/TEAM-ORCHESTRATION.md — the shared planning and execution workflow
- @instructions/TOOL-PI.md — the Pi-specific setup and native project files

Document the product domain, technology stack, repository layout, runtime and
deployment environments, architecture boundaries, conventions, security
risks, exact build/test/lint/format/verification commands, applicable workers
and front doors, model/tool permissions, and unresolved assumptions.

Use the PRD for intended behavior and the repository for verified facts.
Distinguish assumptions from observations and identify any conflicts or
missing information that require a decision. Do not invent project behavior.
```

Use the PRD, repository instructions, architecture notes, and current repository contents to describe:

- the product and domain;
- languages, frameworks, and repository layout;
- runtime, hosting, deployment, OS, browser, cloud, database, and integration environments;
- architecture boundaries, conventions, and security risks;
- build, test, lint, format, and verification commands;
- applicable workers and front doors; and
- assumptions or unresolved questions.

Keep `TEAM-ORCHESTRATION.md` separate. It defines the planning, execution, coordination, and quality-gate workflow; it is not the project-discovery document.

### 8. Generate and validate the project team

Before writing native files, show a compact map of the proposed team containing:

| Area | Required detail |
|---|---|
| Front doors | `pm-agent` (planning), `team-lead` (execution), and separately invoked `pr-agent` (pull-request lifecycle and review), including native entry points |
| Workers | Selected specialists, mission, ownership, inputs, outputs, and handoff |
| Environment tailoring | Planned repository paths, approved stack, runtime/deployment environments, and commands |
| Models | Model assigned to each role, rationale, and fallback (if any) |
| Permissions | Read/write/execute/network/tool access for each role, with least-privilege rationale |

Use a prompt like this to generate the team:

```text
Generate the project team from the PRD, repository context,
`AGENT-GENERATION.md`, `instructions/TEAM-ORCHESTRATION.md`, and the selected
`instructions/TOOL-*.md` adapter. Read the shared worker contracts in
`instructions/agents/` before tailoring any role. Before creating or changing
native files, show the compact team map requested by this workflow. Incorporate
available human feedback, then generate the prompts, skills, agent files, and
tool configuration required by that map. For every resource:
- use real paths and commands already present in the repository, or clearly
  label a path/command as planned for this new project;
- use only technologies, services, models, and environments approved in the PRD
  or explicitly approved by the owner; never fill gaps by guessing;
- follow the repository's naming, placement, formatting, and instruction
  conventions;
- preserve the canonical role mission, ownership, boundaries, evidence,
  quality gates, and handoff protocol from
  `instructions/TEAM-ORCHESTRATION.md` and `instructions/agents/`;
- keep `pm-agent` as the planning front door and `team-lead` as the execution
  front door;
- keep `pr-agent` separately invoked for pull-request lifecycle and cumulative review; it must not run automatically from `team-lead`; and
- use permissions appropriate to its mission and project workflow.

Do not modify application code or unrelated project behavior. If the PRD,
repository, or adapter is ambiguous or contradictory, stop and ask a focused
question rather than inventing behavior.
```

For a new project, a fuller default worker set may be appropriate because there
is no legacy configuration to preserve. Omit roles that are clearly irrelevant,
and record why omitted roles are not needed.

Validate without modifying application code. Record evidence that:

- every planned path, command, and configuration target is either present or
  explicitly identified as a generated project artifact;
- generated prompts, skills, and agents are syntactically valid, use the
  approved stack and repository conventions, and preserve the routing and
  authority rules in `instructions/TEAM-ORCHESTRATION.md`;
- native tool entry points load and delegate to the intended front doors;
- model assignments and permissions match the approved map and least-privilege
  boundaries; and
- the generated diff contains only approved orchestration/setup resources.

Report missing prerequisites, unresolved assumptions, and validation failures;
do not silently add technologies, broaden permissions, or alter application
behavior to make validation pass.

### 9. Run planning mode

Use `pm-agent` to read the PRD and repository context, ask questions where necessary, and create the authoritative first sprint in the configured backend.

Planning should produce:

- scope and exclusions;
- task dependencies and assignments;
- acceptance criteria;
- applicable specialist phases and quality gates;
- deterministic verification commands; and
- any required design, domain, or API artifacts.

### 10. Human approval gate

The human reviews the generated team and the first sprint plan. Resolve material product, architecture, environment, or scope questions before execution.

Build execution begins after the sprint manifest is approved.

### 11. Execute the approved plan

Invoke `team-lead` with the approved sprint. It coordinates the applicable workers, build gates, adversarial review, independent review, commit gate, and final testing according to `TEAM-ORCHESTRATION.md`.

Unattended or multi-sprint execution follows the initial planning and execution loop once its evidence supports that operating mode.

## New-project principle

Be opinionated about structure and quality gates, but keep project behavior grounded in the PRD and verified repository context. A new project allows a complete default setup, but every generated agent must still be tailored to the actual technology and deployment environment.
