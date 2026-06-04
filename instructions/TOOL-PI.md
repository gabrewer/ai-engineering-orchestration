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

## Prompt Templates

Pi prompt templates live in `.pi/prompts/*.md` and become slash commands in interactive mode. Use them for human-facing workflows such as brainstorming, planning, team-lead execution, review, or release checklists.

Example:

```markdown
---
description: Plan a sprint using the user-selected state backend
argument-hint: "<feature-or-prd> <github-issues|filesystem>"
---

Plan a sprint for $1 using state backend $2. Follow instructions/TEAM-ORCHESTRATION.md.
```

Templates support `$1`, `$2`, `$@`, and related positional argument forms.

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

Use Pi extensions in `.pi/extensions/` when agentloop needs custom commands, custom tools, guardrails, or richer integration.

Useful extension ideas for this workflow:

- block writes to `.env`, `node_modules`, `bin`, `obj`, and generated output directories;
- intercept dangerous bash commands and require confirmation;
- register helper commands such as `/agentloop-status` or `/post-agent-update`;
- add custom tools for reading/writing the selected state backend consistently.

Extensions are TypeScript modules and can register tools via `pi.registerTool()` and commands via `pi.registerCommand()`.

---

## Notes

- Pi project context is normally provided by `AGENTS.md` files in the repository tree.
- Pi skills are progressively loaded: startup includes skill names/descriptions, and the agent reads full `SKILL.md` when the task matches or the user invokes `/skill:<name>`.
- Prompt templates are non-recursive under `.pi/prompts/`; put one template per file at that level unless configured otherwise.
- Use `/reload` in interactive Pi after changing skills, prompt templates, extensions, or context files.
- Add `.agentloop/tmp/`, `.agentloop/logs/`, `.agentloop/state-*.json`, and any Pi subprocess session directory to `.gitignore`.
