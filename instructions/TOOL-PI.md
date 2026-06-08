# Tool Configuration: Pi

This file describes how to configure agentloop for use with **Pi** — a minimal terminal coding harness with built-in file/edit/bash tools, project context files, skills, prompt templates, extensions, and print/JSON/RPC modes.

---

## Directory Structure

```
AGENTS.md                   # Repo-wide Pi context/instructions
.pi/
  skills/                   # Project-local skills (root .md files or directories with SKILL.md)
  prompts/                  # Prompt templates (one .md per slash command)
  extensions/               # Optional TypeScript extensions/tools/commands
  settings.json             # Optional Pi resource/model/tool settings
verify/                     # Verification scripts (one subdirectory per feature)
.agentloop/
  tmp/                      # Temporary GitHub issue bodies/comments; never committed
  logs/                     # agentloop diagnostic logs; never committed
  state-*.json              # agentloop run state; never committed
task-issues.json            # Task ID → GitHub issue number mapping (GitHub mode only)
```

In filesystem state-backend mode, durable orchestration state lives in the paths defined by `TEAM-ORCHESTRATION.md`, such as `docs/sprints/`, `docs/reviews/`, and `docs/reports/`.

---

## Agent Definition Format

Pi does not ship a native subagent file format. For agentloop, define each role as either:

1. a **Pi skill** in `.pi/skills/<agent-name>/SKILL.md` or `.agents/skills/<agent-name>/SKILL.md`; or
2. a plain prompt file consumed by agentloop and passed to `pi -p` / `pi --mode json`.

Recommended project-local skill format:

```markdown
---
name: backend-builder
description: Builds backend code for one assigned task. Use when implementing server-side application changes from an agentloop task.
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

If the repo already has project instructions, preserve them and merge Pi-specific guidance into the least surprising place rather than replacing them.

### 2. Add the minimum Pi project assets

Recommended minimal layout:

```text
AGENTS.md                         # Repo-wide Pi instructions; create only if absent
.pi/
  prompts/
    pm-agent.md                   # planning front door
    team-lead.md                  # execution/build-loop front door
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
.agentloop/                       # runtime only; ignored
```

Use `.agents/skills/` for worker identities when you want the same skill files to be reusable by other Agent Skills-compatible harnesses. Use `.pi/skills/` for Pi-only skills. Do not add both unless there is a clear reason.

### 3. Bootstrap `AGENTS.md`

If no repo-level context file exists, create a short `AGENTS.md` with:

```markdown
# Project Instructions

- Follow the existing architecture and conventions in this repository.
- Do not change public contracts, migrations, deployment config, or CI unless the task explicitly requires it.
- Use the repo's documented package manager and build/test commands.
- Keep generated plans, issue drafts, logs, and state out of commits unless they are durable project artifacts.
- Never close GitHub issues or apply final disposition labels; prepare acceptance evidence for a human instead.
```

If `AGENTS.md` or `CLAUDE.md` already exists, append only the Pi/PiLoop deltas and keep the existing project rules authoritative.

### 4. Create worker skills from the project context

Each worker skill should be a directory with `SKILL.md` and frontmatter:

```markdown
---
name: backend-builder
description: Builds backend code for one assigned task in this repository. Use when implementing server-side changes from a PiLoop task.
---

# Backend Builder

Read `AGENTS.md`, `instructions/TEAM-ORCHESTRATION.md`, the sprint issue/file, and the files named in the task before editing. Follow the repository's existing backend architecture and verification commands. Never modify tests unless this task explicitly assigns test work.
```

Keep the first version conservative. Prefer narrow, repository-specific instructions over broad generic agent personas.

### 5. Create the two front-door prompts

Install at least:

- `.pi/prompts/pm-agent.md` — converts a PRD/spec into an audited sprint issue/file.
- `.pi/prompts/team-lead.md` — executes an approved sprint through worker skills and quality gates.

Both prompts must name exact files to read first, the selected state backend, temp-file paths, quality-gate headings, verification commands, and the rule that acceptance verification is prepared for a human rather than self-approved.

### 6. Update ignore rules

Add only missing entries:

```gitignore
.agentloop/tmp/
.agentloop/logs/
.agentloop/state-*.json
.agentloop/pi-sessions/
.pi/tmp/
```

Do not ignore `.pi/prompts/`, `.pi/skills/`, `.agents/skills/`, or durable sprint docs that should be reviewed and committed.

### 7. Smoke-test discovery before using agents

From the repo root:

```bash
pi --no-extensions --tools read,grep,find,ls -p "List the available project skills and prompt templates. Do not edit files."
```

Then test one worker explicitly:

```bash
pi --no-extensions --tools read,grep,find,ls --skill .agents/skills/review-agent/SKILL.md -p "Read the project instructions and summarize the review boundaries. Do not edit files."
```

Fix missing frontmatter, invalid skill names, or path mistakes before planning real work.

---

## Prompt Templates

Pi prompt templates live in `.pi/prompts/*.md` and become slash commands in interactive mode. Use them for human-facing workflows such as brainstorming, planning, team-lead execution, review, or release checklists.

High-quality project workflows should use **thin, project-specific front-door prompts** rather than generic agent invocations. A good `/pm-agent` prompt reads the design/spec, audits source, creates the authoritative sprint issue/file, and defines the task quality bar. A good `/team-lead` prompt runs the build loop itself, loading worker skills by path at each phase and enforcing quality gates before completion.

Example:

```markdown
---
description: Plan a sprint using the user-selected state backend
argument-hint: "<feature-or-prd> <github-issues|filesystem>"
---

Plan a sprint for $1 using state backend $2. Follow instructions/TEAM-ORCHESTRATION.md.
```

Templates support `$1`, `$2`, `$@`, and related positional argument forms.

### Prompt quality checklist

Use the Lessi.App sequence-parity workflow as the target quality bar for generated Pi prompt templates:

- **Read-before-write list**: name exact standards, spec files, source areas, tests, and existing issue comments to read before planning or execution.
- **Single source of truth**: state whether GitHub Issues or filesystem is authoritative. In GitHub mode, prefer one umbrella/control sprint issue with comments/checklists when the human wants to avoid issue sprawl.
- **Full-stack default**: require a Contract Impact Check before tasking. Frontend-only is allowed only when explicitly marked `UI polish only`, `docs only`, or `frontend prototype only`.
- **No state tunneling**: forbid production behavior that hides structured domain state in free-text fields such as `notes`, `description`, `metadataJson`, or local/session storage when a typed API contract is required.
- **Write-side validation**: if typed IDs link persisted resources, require create/update paths to reject malformed, nonexistent, deleted, cross-user/tenant, and invalid child-item references before persistence.
- **Source delta audit**: for parity/migration work, require a matrix with source behavior, target behavior, status, required fix/deviation, and source references before coding.
- **Implementation-ready tasks**: every task names agent, dependencies, files to read/change, acceptance criteria, exact verification command, commit hint, and skills to load.
- **Quality gates as phases**: destroyer, reviewer, and tester are mandatory phases, not ordinary task-board work. The prompt must auto-remediate blockers and rerun gates until pass or threshold.
- **Evidence standard**: final completion must cite commits, source evidence, test evidence, runtime/browser evidence, accepted deviations, and unresolved risks.
- **Acceptance verification gate**: agents must prepare a `Ready for Acceptance Verification` comment with a checklist derived from the original acceptance criteria/scope/design/source-of-truth, manual verification steps, expected results, source references/screenshots/reference pages, unresolved risks, and remaining deltas. Passing tests/commits are not acceptance.
- **No issue closure**: agents must never close GitHub issues or apply final completion/disposition labels such as `done`, `complete`, or `shipped`; post a final summary and acceptance-verification comment only.
- **Temporary files**: compose GitHub bodies/comments under `.pi/tmp/` or `.agentloop/tmp/` and never commit them.

### Recommended project prompt set

For PiLoop-style projects, install at least:

```text
.pi/prompts/pm-agent.md      # spec/design → audited sprint issue/file
.pi/prompts/team-lead.md     # sprint issue/file → build loop + gates + final summary + acceptance checklist
```

The prompt names should match entries in `.pi/skill-models.json` when using model routing.

---

## Tool Permissions

Pi's built-in tools are:

```text
read, bash, edit, write, grep, find, ls
```

For unattended agentloop subprocesses, pass an allowlist appropriate to the role:

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

## agentloop Invocation

Pi supports non-interactive print mode and JSON event mode. agentloop can invoke Pi agents with either.

### Simple print mode

```bash
pi -p \
  --no-prompt-templates \
  --tools read,write,edit,bash,grep,find,ls \
  --append-system-prompt "$(cat .pi/skills/backend-builder/SKILL.md)" \
  "Execute TASK-003 from the selected state backend. Follow instructions/TEAM-ORCHESTRATION.md."
```

### JSON mode for process integration

```bash
pi --mode json \
  --tools read,write,edit,bash,grep,find,ls \
  --append-system-prompt "$(cat .pi/skills/backend-builder/SKILL.md)" \
  "Execute TASK-003 from the selected state backend."
```

Use `--session-dir .agentloop/pi-sessions` if agentloop should keep Pi subprocess sessions separate from normal interactive sessions.

---

## State Backend Rules

Follow `TEAM-ORCHESTRATION.md`: the **user specifies** either GitHub Issues mode or filesystem mode as the state backend. Do not choose autonomously.

- **GitHub Issues mode:** post progress and reports as issue comments. Use `.agentloop/tmp/` for `gh --body-file` drafts and do not commit those drafts.
- **Filesystem mode:** write progress and reports to `docs/sprints/`, `docs/reviews/`, and `docs/reports/` using the same markdown headings.

Pi agents should preserve the selected backend across every prompt, skill, and subprocess invocation. If a subprocess prompt lacks the backend, stop and ask the coordinator to provide it rather than guessing.

---

## Extensions

Use Pi extensions in `.pi/extensions/` when agentloop needs custom commands, custom tools, guardrails, model routing, or richer integration.

Useful extension ideas for this workflow:

- block writes to `.env`, `node_modules`, `bin`, `obj`, and generated output directories;
- intercept dangerous bash commands and require confirmation;
- register helper commands such as `/agentloop-status` or `/post-agent-update`;
- add custom tools for reading/writing the selected state backend consistently;
- route important prompts/skills to stronger models with a shared `.pi/skill-models.json` configuration.

A proven Pi setup uses `.pi/extensions/skill-model-router.ts` plus `.pi/skill-models.json` so `/team-lead`, `/pm-agent`, destroyer, reviewer, tester, and specialized builders get deliberate model/thinking settings. PiLoop subprocesses should read the same routing file directly because raw RPC prompts may not trigger slash-command extension routing.

Extensions are TypeScript modules and can register tools via `pi.registerTool()` and commands via `pi.registerCommand()`.

---

## Notes

- Pi project context is normally provided by `AGENTS.md` files in the repository tree.
- Pi skills are progressively loaded: startup includes skill names/descriptions, and the agent reads full `SKILL.md` when the task matches or the user invokes `/skill:<name>`.
- Prompt templates are non-recursive under `.pi/prompts/`; put one template per file at that level unless configured otherwise.
- Use `/reload` in interactive Pi after changing skills, prompt templates, extensions, or context files.
- Add `.agentloop/tmp/`, `.agentloop/logs/`, `.agentloop/state-*.json`, and any Pi subprocess session directory to `.gitignore`.
