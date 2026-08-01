# Workflow: Set Up Orchestration in an Existing Project

Use this workflow when adding the orchestration system to an established codebase. The goal is to integrate with the project—not replace its architecture, conventions, tools, or delivery process.

## Outcome

The project should have:

- documented repository and environment context;
- project-specific generation instructions;
- only the required tool adapters, prompts, and skills;
- a configured state backend;
- a proven planning workflow; and
- a controlled path to execution.

## Integration considerations

The setup should fit the project’s existing practices. In particular, account for existing instructions, scripts, CI/CD, agent configuration, branching, and release processes. Introduce only the assets and execution authority that the project needs, expanding autonomy as the planning and execution evidence warrants.

## Workflow

### 1. Establish the project context

Identify the target repository, its intended initiative, and the project’s existing development and delivery context. Establish the appropriate branch or checkpoint for the setup work, consistent with the repository’s normal contribution practices.

### 2. Inventory the existing project

Inspect and record:

- repository structure and ownership boundaries;
- languages, frameworks, package managers, and build systems;
- build, test, lint, format, migration, and deployment commands;
- current documentation, PRDs, architecture notes, and runbooks;
- existing CI/CD, branching, release, and review conventions;
- runtime, hosting, cloud, database, browser, OS, and integration environments;
- security, privacy, compliance, and operational constraints;
- existing AI instructions, prompts, skills, agents, and tool configuration; and
- known gaps, risks, and assumptions.

Use this inventory to establish the boundaries for project-specific agent generation.

### 3. Select or create the source of truth

Identify the PRD, design brief, issue, architecture document, or other source that describes the initiative to be planned.

If the initiative is new, use discovery and brainstorming with the human owner, then create or refine a PRD under `docs/`. If it is already planned, select the existing authoritative source.

Record conflicts between the source documentation and the repository, with the resolution captured as a project decision.

### 4. Create the setup boundary

Decide which files may be added or changed and record the baseline. Keep the existing project structure authoritative unless the owner approves a change.

Record enough baseline information to distinguish orchestration changes from pre-existing work, using the repository’s normal change-management practices.

### 5. Copy the baseline orchestration contracts

Copy the shared baseline assets from this repository where the target project does not already provide an equivalent:

- `instructions/AGENT-GENERATION.md`;
- `instructions/TEAM-ORCHESTRATION.md`;
- `instructions/agents/`; and
- the selected tool adapter from `instructions/TOOL-*.md`.

If equivalent files already exist, compare them and preserve project-specific rules. The baseline files define shared contracts; project-specific details belong in the project generation instructions and native tool files.

### 6. Select and configure the AI tool

Choose the tool or tools for this project, such as Claude Code, GitHub Copilot, Codex, Pi, or opencode. For each selected tool:

- read the corresponding `TOOL-*.md` adapter;
- verify the adapter exists, creating one if necessary;
- install or generate its native project files and entry points;
- configure model, delegation, permissions, and tool access; and
- verify that `pm-agent` owns planning and `team-lead` owns execution.

Adapters define native setup and delegation. Shared worker contracts and orchestration gates remain defined by the canonical instructions.

### 7. Configure the state backend

During one-time repository setup, the project owner selects:

- `github-issues` for GitHub-backed planning and auditability; or
- `filesystem` for local or private planning artifacts.

Persist the choice in `AGENTS.md` (or the harness-equivalent repository context file) as:

```markdown
**State backend:** github-issues
```

or:

```markdown
**State backend:** filesystem
```

If a marker already exists, use it. A change to the backend for active work should be handled as a separate migration decision.

### 8. Create project-specific generation instructions

Create or refine `AGENT-GENERATION.md` using the inventory and source-of-truth documents. A useful starting prompt is:

```text
Review this existing repository and create or refine AGENT-GENERATION.md.

Read these orchestration references first:
- @instructions/AGENT-GENERATION.md — what agent generation must accomplish
- @instructions/TEAM-ORCHESTRATION.md — the shared planning and execution workflow
- @instructions/TOOL-PI.md — the Pi-specific setup and native project files

Then inspect the repository instructions, source tree, documentation, tests,
package/build configuration, CI/CD, deployment configuration, and existing AI
configuration. Do not modify application code.

Document the project domain, architecture boundaries, technology stack,
runtime and deployment environments, repository conventions, security and
operational constraints, exact build/test/lint/format commands, applicable
front-door and worker agents, model/tool permissions, and unresolved
assumptions. Use evidence from specific files and distinguish observed facts
from inferences. Preserve existing project rules and ask focused questions for
material ambiguities.
```

It should identify:

- project domain and terminology;
- architecture and ownership boundaries;
- approved technologies and environments;
- repository paths and conventions;
- exact build, test, lint, format, and verification commands;
- security and operational risks;
- applicable workers and front doors;
- model and permission constraints; and
- assumptions or unresolved questions.

Keep `TEAM-ORCHESTRATION.md` focused on planning, execution, coordination, and quality gates.

### 9. Generate and validate the project team

Show a compact map of the proposed front doors, workers, environment tailoring, model assignments, and permissions before generating native files.

Generate the prompts, skills, and agent files required by the project. They should:

- reference real project paths and commands;
- follow existing repository conventions;
- avoid assuming unapproved technologies;
- preserve existing agent behavior where it remains valid; and
- retain the canonical role mission, ownership, boundaries, evidence, and handoff.

Validate discovery and generated resources without modifying application code. Confirm that commands resolve, tool entry points load, permissions are appropriate, and unrelated project behavior is unchanged.

### 10. Run planning-only mode

Use `pm-agent` to read the selected source of truth and repository context. Planning must:

- resolve the persisted state backend without another choice prompt;
- perform the Contract Impact Check;
- create the authoritative sprint or issue record;
- identify dependencies, acceptance criteria, and verification commands;
- ask questions where ambiguity blocks safe planning; and
- stop before implementation.

### 11. Human review gate

The human reviews the generated team, setup diff, source assumptions, state backend, and first sprint plan. Resolve material conflicts before execution.

### 12. Enable execution gradually

After planning is approved, enable execution in stages:

1. test-writer and limited builder tasks;
2. build and deterministic verification gates;
3. destroyer and independent review;
4. commit and final smoke-test flow; and
5. multi-sprint or unattended execution when the preceding stages provide evidence for that operating mode.

Each stage must produce evidence and remain reversible.

## Existing-project principle

The goal is not to make the repository look like the orchestration system. The goal is to make the orchestration system work safely inside the repository that already exists.
