# Tool Configuration: Pi

This file describes how to configure the team-orchestration workflow for **Pi** — a minimal terminal coding harness with built-in file/edit/bash tools, project context files, skills, prompt templates, and extensions.

## Prompt and Skill Topology

The Pi workflow has two required front doors:

- `/pm-agent` owns the end-to-end planning experience.
- `/team-lead` owns the end-to-end execution experience.

Worker specialization belongs primarily in skills loaded by those prompts. Avoid mechanically turning every internal phase into another prompt, but allow additional prompts when they represent a clear standalone workflow or useful operator command. Before generating project resources, show the proposed prompt-to-skill map and confirm that planning and execution remain understandable without exposing orchestration internals to the user.

Project-specific guidance may specialize this topology. Treat a change to front-door ownership as an intentional design decision, not an accidental consequence of a generated blueprint.

## Prepare Project-Specific Context

Before generating Pi prompts and skills, ask the active agent to create a short project-specific context document. This document should help adapt the generic orchestration roles to the project without becoming a second workflow specification.

For work driven by a PRD or design document, use a prompt like:

> Okay, now let's examine the work we need to do in `@<path-to-prd>` and create some documentation to help `@instructions/TEAM-ORCHESTRATION.md` and `@instructions/TOOL-PI.md` create great skills and prompts for this project.

For an existing project, have the agent inspect the codebase instead:

> Okay, now let's review the existing codebase, including its architecture, build and test commands, conventions, and current agent instructions, and create some documentation to help `@instructions/TEAM-ORCHESTRATION.md` and `@instructions/TOOL-PI.md` create great skills and prompts for this project.

Adjust the referenced paths if the instruction files were copied somewhere else. Review the resulting project context before asking the agent to propose or generate the actual prompt and skill resources.

---

## Directory Structure

```
AGENTS.md                   # Repo-wide Pi context/instructions
.pi/
  skills/                   # Project-local skills (root .md files or directories with SKILL.md)
  prompts/                  # Prompt templates (one .md per slash command)
  extensions/               # Optional TypeScript extensions/tools/commands
  settings.json             # Optional Pi resource/model/tool settings
  tmp/                      # Temporary GitHub issue bodies/comments; never committed
verify/                     # Verification scripts (one subdirectory per feature)
task-issues.json            # Task ID → GitHub issue number mapping (GitHub mode only)
```

In filesystem state-backend mode, durable orchestration state lives in the paths defined by `TEAM-ORCHESTRATION.md`, such as `docs/sprints/`, `docs/reviews/`, and `docs/reports/`.

---

## Agent Definition Format

Pi does not ship a native subagent file format. Define each worker role as a **Pi skill** in `.pi/skills/<agent-name>/SKILL.md` or `.agents/skills/<agent-name>/SKILL.md`. The `/team-lead` prompt loads the appropriate skill when each phase begins and applies it within the active session.

Recommended project-local skill format:

```markdown
---
name: backend-builder
description: Builds backend code for one assigned task. Use when implementing server-side application changes from an approved orchestration task.
---

You are the backend-builder...
```

Skill names must be lowercase letters, numbers, and hyphens. Pi discovers project skills from `.pi/skills/` and `.agents/skills/`, and users can force-load one with `/skill:<name>` in interactive mode or `--skill <path>` in CLI mode.

---

## Adding Pi Agents to an Existing Project

Use this path when a repo already has code, docs, tests, and conventions. The goal is to add Pi agent orchestration without reshaping the repository around a template.

### 1. Inspect before installing

Before adding files, inspect and record:

- language/framework stack and project layout;
- build, test, lint, and run commands;
- existing `AGENTS.md`, `CLAUDE.md`, `.github/`, `docs/`, `.pi/`, `.agents/`, or tool-specific agent files;
- current branching and PR expectations;
- existing issue labels, milestones, and release workflow;
- where temporary files and generated artifacts must not be written.

Do not invent solution, project, package, test, or verification paths. If a workstream has no build surface yet, make the first scaffold/build command an explicit bootstrap task and plan later work from the resulting real structure.

If the repo already has project instructions, preserve them and merge Pi-specific guidance into the least surprising place rather than replacing them.

### 2. Add the minimum Pi project assets

Recommended minimal layout:

```text
AGENTS.md                         # Repo-wide Pi instructions; create only if absent
.pi/
  prompts/
    pm-agent.md                   # planning front door
    team-lead.md                  # execution/build-loop front door
    pr-checkpoint.md               # on-demand branch growth / PR boundary report
.agents/
  skills/
    product-designer/SKILL.md
    pm/SKILL.md
    domain-modeler/SKILL.md
    api-developer/SKILL.md
    test-writer/SKILL.md
    backend-builder/SKILL.md
    frontend-builder/SKILL.md
    destroyer/SKILL.md
    review-agent/SKILL.md
    git-committer/SKILL.md
.pi/tmp/                          # temporary drafts only; ignored
```

Use `.agents/skills/` for worker identities when you want the same skill files to be reusable by other Agent Skills-compatible harnesses. Use `.pi/skills/` for Pi-only skills. Do not add both unless there is a clear reason.

### 3. Bootstrap `AGENTS.md`

During this one-time bootstrap, ask which durable orchestration backend the repository will use (`github-issues` or `filesystem`). Persist the answer in `AGENTS.md`; planning prompts and workers read it from there and must not ask again.

If no repo-level context file exists, create a short `AGENTS.md` with:

```markdown
# Project Instructions

**State backend:** github-issues | filesystem

- Follow the existing architecture and conventions in this repository.
- Do not change public contracts, migrations, deployment config, or CI unless the task explicitly requires it.
- Use the repo's documented package manager and build/test commands.
- Keep generated plans, issue drafts, logs, and state out of commits unless they are durable project artifacts.
- Never close GitHub issues or apply final disposition labels; prepare acceptance evidence for a human instead.
- Apply the pull-request size checkpoints from `instructions/TEAM-ORCHESTRATION.md`; recommend review before branch scope becomes difficult to audit.
```

Replace the placeholder with the user's choice. If `AGENTS.md` or the harness-equivalent repo context file already exists, append the single selected marker and only the Pi orchestration deltas; keep the existing project rules authoritative. If agents were installed previously without a marker, ask once on the next setup/planning run and persist it immediately.

### 4. Create worker skills from the project context

Each worker skill should be a directory with `SKILL.md` and frontmatter:

```markdown
---
name: backend-builder
description: Builds backend code for one assigned task in this repository. Use when implementing server-side changes from an approved sprint task.
---

# Backend Builder

Use the automatically loaded repository instructions, then read `instructions/TEAM-ORCHESTRATION.md`, the sprint issue/file, and the files named in the task before editing. Follow the repository's existing backend architecture and verification commands. Never modify tests unless this task explicitly assigns test work.
```

Keep the first version conservative. Prefer narrow, repository-specific instructions over broad generic agent personas. Every generated worker skill and plain agent prompt must include a short rule to reuse the state backend from automatically loaded repository instructions without prompting, and to report incomplete initialization to the coordinator if it is absent. Do not copy the selected value into each agent, require a separate `AGENTS.md` read, or limit this knowledge to `/pm-agent` and `/team-lead`.

### 5. Create the two front-door prompts

Install both:

- `.pi/prompts/pm-agent.md` — converts a PRD/spec into an audited sprint issue/file.
- `.pi/prompts/team-lead.md` — executes an approved sprint through worker skills and quality gates.

Both prompts must use the automatically loaded repo-level instructions containing the persisted state backend, name any additional files to read first, and include temp-file paths, quality-gate headings, verification commands, and the rule that acceptance verification is prepared for a human rather than self-approved. They must resolve the backend from repository context rather than accepting it as a routine prompt argument or spending a separate tool call to reload `AGENTS.md`.

Before creating prompts and skills, present a concise resource map showing each prompt, the worker skills it coordinates, and the proposed provider/model/thinking assignment. This is a design review, not a requirement to forbid additional prompts.

### 6. Update ignore rules

Add only missing entries:

```gitignore
.pi/tmp/
```

Do not ignore `.pi/prompts/`, `.pi/skills/`, `.agents/skills/`, or durable sprint docs that should be reviewed and committed.

### 7. Smoke-test discovery before using agents

From the repo root:

```bash
pi --approve --no-extensions --tools read,grep,find,ls -p "List the available project skills and prompt templates. Summarize which prompt owns planning, which owns execution, and whether any optional prompt has overlapping responsibility. Do not edit files."
```

Then test one worker explicitly:

```bash
pi --approve --no-extensions --tools read,grep,find,ls --skill .agents/skills/review-agent/SKILL.md -p "Read the project instructions and summarize the review boundaries. Do not edit files."
```

Fix missing frontmatter, invalid skill names, or path mistakes before planning real work.

---

## Prompt Templates

Pi prompt templates live in `.pi/prompts/*.md` and become slash commands in interactive mode. Use them for human-facing workflows such as brainstorming, planning, team-lead execution, review, or release checklists.

High-quality project workflows should use **thin, project-specific front-door prompts** rather than generic agent invocations. A good `/pm-agent` prompt reads the design/spec, audits source, creates the authoritative sprint issue/file, and defines the task quality bar. A good `/team-lead` prompt runs the build loop itself, loading worker skills by path at each phase and enforcing the canonical gates from `TEAM-ORCHESTRATION.md` before completion.

Use worker skills for internal phases by default. Add another prompt when it gives the user a distinct workflow or operator utility, and state how it relates to the two primary front doors.

Example:

```markdown
---
description: Plan a sprint using the repository's configured state backend
argument-hint: "<feature-or-prd>"
---

Use the configured state backend from the automatically loaded repository instructions. Plan a sprint for $1 and follow instructions/TEAM-ORCHESTRATION.md. Do not ask for the backend when it is configured.
```

Templates support `$1`, `$2`, `$@`, and related positional argument forms.

### Prompt quality checklist

Use the Lessi.App sequence-parity workflow as the target quality bar for generated Pi prompt templates:

- **Read-before-write list**: name exact standards, spec files, source areas, tests, and existing issue comments to read before planning or execution.
- **Single source of truth**: read the repository's persisted backend marker and state whether GitHub Issues or filesystem is authoritative; do not make it a per-run choice. In GitHub mode, prefer one umbrella/control sprint issue with comments/checklists when the human wants to avoid issue sprawl.
- **Full-stack default**: require a Contract Impact Check before tasking. Frontend-only is allowed only when explicitly marked `UI polish only`, `docs only`, or `frontend prototype only`.
- **No state tunneling**: forbid production behavior that hides structured domain state in free-text fields such as `notes`, `description`, `metadataJson`, or local/session storage when a typed API contract is required.
- **Write-side validation**: if typed IDs link persisted resources, require create/update paths to reject malformed, nonexistent, deleted, cross-user/tenant, and invalid child-item references before persistence.
- **Risk-based backend testing**: follow repository test conventions; require automated integration tests—not unit tests alone—when behavior depends on HTTP, auth/ownership/tenancy, persistence, concurrency, messaging, service discovery, gateways, or other runtime boundaries.
- **Source delta audit**: for parity/migration work, require a matrix with source behavior, target behavior, status, required fix/deviation, and source references before coding.
- **Implementation-ready tasks**: every task names agent, dependencies, files to read/change, acceptance criteria, exact verification command, commit hint, and skills to load.
- **PR size checkpoints**: `/pm-agent` plans reviewable PR slices, `/team-lead` measures before/after coherent batches, and `git-committer` reports the canonical `BELOW`/`ADVISORY`/`STRONG` checkpoint from `TEAM-ORCHESTRATION.md`.
- **Quality gates as phases**: prompt templates must enforce the canonical destroyer, reviewer, committer, tester/smoke, readiness, and issue-disposition gates from `TEAM-ORCHESTRATION.md`.
- **Evidence standard**: final completion must cite the evidence required by `TEAM-ORCHESTRATION.md` without redefining it locally.
- **Acceptance verification gate**: agents must prepare the `Ready for Acceptance Verification` artifact defined by `TEAM-ORCHESTRATION.md`; passing tests/commits are implementation evidence only, not acceptance.
- **No issue closure**: agents must follow the canonical issue-disposition rules in `TEAM-ORCHESTRATION.md`.
- **Temporary files**: compose GitHub bodies/comments under `.pi/tmp/` and never commit them.

### Recommended project prompt set

For this orchestration style, install at least:

```text
.pi/prompts/pm-agent.md      # spec/design → audited sprint issue/file
.pi/prompts/team-lead.md     # sprint issue/file → build loop + gates + final summary + acceptance checklist
.pi/prompts/pr-checkpoint.md  # branch/base → commits/files + review-boundary recommendation
```

The prompt names should match entries in `.pi/skill-models.json` when using model routing.

### Model assignments

Run `pi --list-models` in the target environment before choosing routes. For every installed prompt and skill, record an exact provider, model ID, and thinking level in the proposed resource map and in a shared `.pi/skill-models.json` when model routing is enabled. Do not copy model IDs from another machine without verifying availability.

Keep the mapping auditable: config keys should match prompt filenames and skill names. Interactive routing may use an extension, and the resource map should document any intentional model changes between front-door prompts and worker skills.

---

## Tool Permissions

Pi's built-in tools are:

```text
read, bash, edit, write, grep, find, ls
```

When a prompt, skill, or extension can restrict tools by role, use an allowlist appropriate to the work:

| Role | Suggested tools |
|------|-----------------|
| product-designer / pm | `read,write,edit,bash,grep,find,ls` |
| domain-modeler / api-developer | `read,write,edit,bash,grep,find,ls` |
| backend-builder / frontend-builder | `read,write,edit,bash,grep,find,ls` |
| test-writer | `read,write,edit,bash,grep,find,ls` |
| destroyer | `read,write,bash,grep,find,ls` if writing adversarial tests; otherwise omit `write` |
| review-agent | `read,bash,grep,find,ls` |
| git-committer | `read,bash,grep,find,ls` |

Use `--tools` to restrict tools:

```bash
pi -p --tools read,bash,grep,find,ls "Review this task without editing files"
```

---

## Interactive Execution

Run orchestration through the Pi front-door prompts:

- `/pm-agent <feature-or-prd>` plans the work with the repository's configured state backend and prepares the authoritative sprint record.
- `/team-lead <sprint-or-feature-id>` executes an approved plan with the configured state backend through the worker skills and canonical quality gates.

The Team Lead reads each required `SKILL.md` before adopting that worker role. It returns to the coordinator role between phases and updates the selected state backend. If a project later adds an extension for isolated delegation, that extension must preserve the same worker contracts, tool restrictions, and evidence rules.

---

## State Backend Rules

Follow `TEAM-ORCHESTRATION.md`: the user chooses GitHub Issues mode or filesystem mode once during repository agent initialization, and setup persists that choice in `AGENTS.md` (or the harness-equivalent committed context file). Do not choose autonomously or request the choice on each run.

- **GitHub Issues mode:** post progress and reports as issue comments. Use `.pi/tmp/` for `gh --body-file` drafts and do not commit those drafts.
- **Filesystem mode:** write progress and reports to `docs/sprints/`, `docs/reviews/`, and `docs/reports/` using the same markdown headings.

Pi prompts and skills use the configured backend from the automatically loaded repository instructions through every phase. A missing worker argument is not a reason to ask the user again. If the persisted marker itself is absent, report incomplete setup to the Team Lead so the one-time initialization can be completed.

---

## Extensions

Use Pi extensions in `.pi/extensions/` when the workflow needs custom commands, custom tools, guardrails, model routing, or richer integration.

Useful extension ideas for this workflow:

- block writes to `.env`, `node_modules`, `bin`, `obj`, and generated output directories;
- intercept dangerous bash commands and require confirmation;
- register helper commands such as `/team-status` or `/post-agent-update`;
- add custom tools for reading/writing the selected state backend consistently;
- route important prompts/skills to stronger models with a shared `.pi/skill-models.json` configuration.

A proven Pi setup uses `.pi/extensions/skill-model-router.ts` plus `.pi/skill-models.json` so `/team-lead`, `/pm-agent`, destroyer, reviewer, tester, and specialized builders get deliberate model/thinking settings.

Extensions are TypeScript modules and can register tools via `pi.registerTool()` and commands via `pi.registerCommand()`.

---

## Notes

- Pi project context is normally provided by `AGENTS.md` files in the repository tree.
- Pi skills are progressively loaded: startup includes skill names/descriptions, and the agent reads full `SKILL.md` when the task matches or the user invokes `/skill:<name>`.
- Prompt templates are non-recursive under `.pi/prompts/`; put one template per file at that level unless configured otherwise.
- Use `/reload` in interactive Pi after changing skills, prompt templates, extensions, or context files.
- Add `.pi/tmp/` and any other project-specific runtime-only directories to `.gitignore`.
