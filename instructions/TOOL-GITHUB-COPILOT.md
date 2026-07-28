# Tool Configuration: GitHub Copilot

This file describes how to configure the team-orchestration workflow for **GitHub Copilot** (VS Code, CLI, or coding agent).

---

## Directory Structure

```
.github/
  agents/
    pm-agent.agent.md       # Planning front door
    team-lead.agent.md      # Execution front door
    product-designer.agent.md
    pm.agent.md
    domain-modeler.agent.md
    api-developer.agent.md
    test-writer.agent.md
    backend-builder.agent.md
    frontend-builder.agent.md
    destroyer.agent.md
    review-agent.agent.md
    git-committer.agent.md
  instructions/             # Shared instruction files (.instructions.md)
  copilot-instructions.md   # Always-loaded rules and front-door routing
  tmp/                      # Temporary GitHub issue bodies/comments; never committed
.agents/
  skills/                   # Shared Agent Skills packages (directories with SKILL.md)
verify/                     # Verification scripts (one subdirectory per feature)
task-issues.json            # Task ID → GitHub issue number mapping (GitHub mode only)
```

---

## Agent Definition Format

Each agent is a markdown file in `.github/agents/` with the `.agent.md` extension and YAML frontmatter:

```markdown
---
name: Backend Builder
description: Builds API endpoints, domain logic, data access, and infrastructure.
model: gpt-5.2
---

Your agent system prompt here.
```

### Model names

| Role | Model |
|------|-------|
| Default (most agents) | `gpt-5.2` |
| High-capability agents (e.g., `product-designer`) | `claude-opus-4.6` or `gpt-5.4` |

Any model available in your GitHub Copilot subscription can be specified by its model ID.

### Required front-door agents

Install both `.github/agents/pm-agent.agent.md` and `.github/agents/team-lead.agent.md`:

- `pm-agent` owns planning, source audits, questions, state-backend setup, sprint artifacts, and the human approval handoff. It must not implement the plan.
- `team-lead` accepts only an approved plan and owns worker delegation, quality gates, commits, reporting, and acceptance preparation.

Both agents must read `.github/copilot-instructions.md` and `TEAM-ORCHESTRATION.md` first, state their entry conditions, and use the canonical progress/report headings.

Add the following to `.github/copilot-instructions.md`:

```markdown
## Orchestration Routing

- Route planning, decomposition, sprint creation, and "plan this" requests to `pm-agent`.
- Route approved-plan execution and "execute the plan" requests to `team-lead`.
- The primary Copilot session must not perform, imitate, collapse, or bypass either front-door workflow.
- `pm-agent` stops at human approval; `team-lead` refuses execution without an approved plan or explicit human override.
```

### Tool permissions

Tool access is controlled via Copilot settings and the agent description — there is no explicit `tools` frontmatter field as in Claude Code. Grant or restrict tool access in your VS Code Copilot settings or repository policy.

---

## Skills / Instructions

- **Global instructions**: `.github/copilot-instructions.md` — applies to all Copilot interactions in the repo.
- **Task-specific instructions**: `.github/instructions/<name>.instructions.md` — scoped instructions for specific file patterns or workflows.
- **Agent Skills**: `.agents/skills/<skill-name>/SKILL.md` — shared skill packages using the Agent Skills `SKILL.md` format.
- **Brainstorming skill**: `.agents/skills/brainstorming/SKILL.md`

---

## Verification Scripts

Place verification shell scripts at `verify/<feature-name>/<task-id>.sh`.

---

## Native Delegation

The primary Copilot session routes the request to the appropriate front-door agent:

```text
/runSubagent pm-agent "<planning request, state-backend choice, and source context>"
/runSubagent team-lead "<approved plan, selected state backend, and execution constraints>"
```

The delegated `team-lead` then invokes worker agents with the task identifier, selected state backend, files to read, and expected report format. Record each agent result in the selected state backend before advancing to the next gate. If required delegation is unavailable, stop instead of collapsing the workflow into the primary session.

---

## Notes

- Agent files must be placed directly in `.github/agents/` — subdirectories are not recognized.
- `pm-agent.agent.md` and `team-lead.agent.md` are required; worker agents do not replace them.
- Keep the routing block in `.github/copilot-instructions.md` explicit so it applies in every primary Copilot session.
- Agent Skills live under `.agents/skills/<skill-name>/SKILL.md`; each skill should be a directory containing `SKILL.md` and any supporting references/scripts/assets.
- The `task-issues.json` file is created during brainstorming and lives at the project root.
- Copilot's coding agent can be assigned tasks directly through GitHub Issues by assigning the issue to `@copilot`.
- Follow `TEAM-ORCHESTRATION.md` as the canonical, harness-agnostic workflow; this file is only the GitHub Copilot adapter for paths, formats, and native delegation.
- Do not restate or override canonical state-backend, quality-gate, commit-gate, readiness, or issue-disposition rules here.
