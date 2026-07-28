# Tool Configuration: Claude Code

This file describes how to configure the team-orchestration workflow for **Claude Code**.

---

## Directory Structure

```
CLAUDE.md                         # Always-loaded project rules and front-door routing
.claude/
  agents/
    pm-agent.md                   # Planning front door
    team-lead.md                  # Execution front door
    product-designer.md           # Worker agents
    pm.md
    domain-modeler.md
    api-developer.md
    test-writer.md
    backend-builder.md
    frontend-builder.md
    destroyer.md
    review-agent.md
    git-committer.md
  skills/                         # Reusable skill definitions (e.g., brainstorming.md)
  verify/                         # Verification scripts (one subdirectory per feature)
  tmp/                            # Temporary GitHub issue bodies/comments; never committed
task-issues.json                  # Task ID → GitHub issue number mapping (GitHub mode only)
```

---

## Agent Definition Format

Each agent is a markdown file in `.claude/agents/` with YAML frontmatter:

```markdown
---
model: sonnet
tools: Read,Write,Edit,Glob,Grep,Bash
---

Your agent system prompt here.
```

### Model names

| Role | Model |
|------|-------|
| Default (most agents) | `sonnet` |
| High-capability agents (e.g., `product-designer`) | `opus` |

### Available tools

`Read`, `Write`, `Edit`, `Glob`, `Grep`, `Bash`, `WebFetch`, `TodoRead`, `TodoWrite`

---

## Required Front-Door Agents

Install both agents:

- `.claude/agents/pm-agent.md` owns planning, source audits, questions, state-backend setup, sprint artifacts, and the human approval handoff. It must not implement the plan.
- `.claude/agents/team-lead.md` accepts only an approved plan and owns worker delegation, deterministic gates, commits, reporting, and acceptance preparation.

Both files must name `TEAM-ORCHESTRATION.md` and `CLAUDE.md` in their read-first instructions, define their entry conditions, and emit the canonical progress/report headings. Give `pm-agent` only the tools needed to inspect the repository and write planning artifacts. Give `team-lead` the tools needed to delegate workers and enforce the complete execution loop.

Add this routing rule to the repository's always-loaded `CLAUDE.md`:

```markdown
## Orchestration Routing

- Planning, decomposition, sprint creation, and "plan this" requests must be delegated to the `pm-agent` agent.
- Approved-plan execution and "execute the plan" requests must be delegated to the `team-lead` agent.
- The primary Claude session must not perform, imitate, collapse, or bypass either front-door workflow.
- `pm-agent` must stop at the approval handoff. `team-lead` must refuse work without an approved plan or an explicit human override.
```

Do not weaken this rule with a fallback that lets the primary session adopt the roles itself. The explicit delegation boundary is part of the workflow, not a suggestion.

---

## Skills

Skills are markdown files in `.claude/skills/`. They are invoked by name in the main session (e.g., `/brainstorming`).

- Brainstorming skill: `.claude/skills/brainstorming.md`

---

## Verification Scripts

Place verification shell scripts at `.claude/verify/<feature-name>/<task-id>.sh`.

---

## Native Delegation

The primary Claude Code session is a router, not the Team Lead. It must delegate planning to `pm-agent` and approved-plan execution to `team-lead`.

Once delegated, `team-lead` coordinates the worker agents in `.claude/agents/`: it selects the next worker, supplies the task and state-backend context, enforces tool boundaries, and records each result before continuing. If required native delegation is unavailable, stop and report that the configured orchestration workflow cannot run; do not silently execute the workflow in the primary session.

---

## Notes

- Agent files must be in `.claude/agents/` — subdirectories are not supported.
- `pm-agent.md` and `team-lead.md` are required; worker agents do not replace them.
- Keep the routing block in `CLAUDE.md` short, explicit, and mandatory so it remains visible in every primary session.
- The `task-issues.json` file is created during brainstorming and lives at the project root.
- Follow `TEAM-ORCHESTRATION.md` as the canonical, harness-agnostic workflow; this file is only the Claude Code adapter for paths, formats, and native delegation.
- Do not restate or override canonical state-backend, quality-gate, commit-gate, readiness, or issue-disposition rules here.
