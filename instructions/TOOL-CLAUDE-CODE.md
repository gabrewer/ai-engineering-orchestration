# Tool Configuration: Claude Code

This file describes how to configure agentloop for use with **Claude Code**.

---

## Directory Structure

```
.claude/
  agents/           # Agent definition files (one .md per agent)
  skills/           # Reusable skill definitions (e.g., brainstorming.md)
  verify/           # Verification scripts (one subdirectory per feature)
task-issues.json    # Task ID → GitHub issue number mapping
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

## Skills

Skills are markdown files in `.claude/skills/`. They are invoked by name in the main session (e.g., `/brainstorming`).

- Brainstorming skill: `.claude/skills/brainstorming.md`

---

## Verification Scripts

Place verification shell scripts at `.claude/verify/<feature-name>/<task-id>.sh`.

---

## agentloop Invocation

agentloop invokes Claude Code agents using the headless flag:

```bash
claude -p "<agent-system-prompt>" --allowedTools "<tools>"
```

The `--allowedTools` flag corresponds to the `tools` list in the agent's YAML frontmatter. agentloop reads the agent definition file and constructs this command automatically.

---

## Notes

- Agent files must be in `.claude/agents/` — subdirectories are not supported.
- The `task-issues.json` file is created during brainstorming and lives at the project root.
