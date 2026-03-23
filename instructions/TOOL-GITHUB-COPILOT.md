# Tool Configuration: GitHub Copilot

This file describes how to configure agentloop for use with **GitHub Copilot** (VS Code, CLI, or coding agent).

---

## Directory Structure

```
.github/
  agents/                   # Agent definition files (one .agent.md per agent)
  instructions/             # Shared instruction files (.instructions.md)
  copilot-instructions.md   # Global repo-wide Copilot instructions
verify/                     # Verification scripts (one subdirectory per feature)
task-issues.json            # Task ID → GitHub issue number mapping
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

### Tool permissions

Tool access is controlled via Copilot settings and the agent description — there is no explicit `tools` frontmatter field as in Claude Code. Grant or restrict tool access in your VS Code Copilot settings or repository policy.

---

## Skills / Instructions

- **Global instructions**: `.github/copilot-instructions.md` — applies to all Copilot interactions in the repo.
- **Task-specific instructions**: `.github/instructions/<name>.instructions.md` — scoped instructions for specific file patterns or workflows.
- **Brainstorming skill**: `.github/instructions/brainstorming.instructions.md`

---

## Verification Scripts

Place verification shell scripts at `verify/<feature-name>/<task-id>.sh`.

---

## agentloop Invocation

agentloop invokes GitHub Copilot agents using the subagent delegation mechanism:

```
/runSubagent <agent-name> "<prompt>"
```

Or programmatically via the GitHub Copilot CLI or VS Code extension API. Configure the invocation command in agentloop's tool configuration (see `tools/agentloop/config/`).

---

## Notes

- Agent files must be placed directly in `.github/agents/` — subdirectories are not recognized.
- The `task-issues.json` file is created during brainstorming and lives at the project root.
- Copilot's coding agent can be assigned tasks directly via GitHub Issues (assign the issue to `@copilot`), which is an alternative to agentloop-driven invocation.
