# Tool Configuration: opencode

This file describes how to configure agentloop for use with **opencode** — the open-source terminal-first AI coding agent.

---

## Directory Structure

```
opencode.json       # Primary opencode configuration (agents defined here)
verify/             # Verification scripts (one subdirectory per feature)
task-issues.json    # Task ID → GitHub issue number mapping
```

opencode uses a central JSON configuration file rather than per-agent markdown files.

---

## Agent Definition Format

Agents are defined in `opencode.json` (or `.opencode/config.json`) under the `agent` key:

```json
{
  "agent": {
    "backend-builder": {
      "description": "Builds API endpoints, domain logic, data access, and infrastructure.",
      "mode": "subagent",
      "model": "anthropic/claude-opus-4-5",
      "tools": {
        "read": true,
        "write": true,
        "edit": true,
        "glob": true,
        "grep": true,
        "bash": true
      }
    },
    "destroyer": {
      "description": "Adversarial reviewer — stress-tests completed work.",
      "mode": "subagent",
      "model": "anthropic/claude-opus-4-5",
      "tools": {
        "read": true,
        "write": true,
        "edit": false,
        "bash": true
      }
    }
  }
}
```

### Mode values

| Mode | Use |
|------|-----|
| `primary` | Main coding agent (e.g., the interactive Team Lead session) |
| `subagent` | Delegated agent invoked by the orchestrator |

### Model names

opencode supports any provider/model combination using the `provider/model-id` format:

| Role | Example model |
|------|--------------|
| Default (most agents) | `anthropic/claude-sonnet-4-5` |
| High-capability agents | `anthropic/claude-opus-4-5` or `openai/gpt-5.4` |
| Read-only / lightweight | `openai/gpt-5-mini` |

---

## Skills

opencode does not have a native skill file format. Use the agent `description` and system prompt fields to encode skill behavior. For the brainstorming skill, define a `brainstorming` agent in `opencode.json` with mode `primary` and the brainstorming system prompt.

---

## Verification Scripts

Place verification shell scripts at `verify/<feature-name>/<task-id>.sh`.

---

## agentloop Invocation

agentloop invokes opencode subagents using the CLI with `@mention` syntax:

```bash
opencode "<prompt>" @<agent-name>
```

For example:
```bash
opencode "Write tests for the user authentication task" @test-writer
```

Configure the invocation command and agent name mapping in agentloop's tool configuration (see `tools/agentloop/config/`).

---

## Notes

- opencode's permission system (`Ask`/`Allow`/`Deny` per action) should be configured to allow unattended execution for subagents used in the build loop.
- The `task-issues.json` file is created during brainstorming and lives at the project root.
- Model provider keys (e.g., `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`) must be set in your environment before running agentloop.
