# Tool Configuration: opencode

This file describes how to configure the team-orchestration workflow for **opencode** — the open-source terminal-first AI coding agent.

---

## Directory Structure

```
AGENTS.md            # Always-loaded project rules and front-door routing
opencode.json       # Primary opencode configuration (agents defined here)
verify/             # Verification scripts (one subdirectory per feature)
.opencode/tmp/      # Temporary GitHub issue bodies/comments; never committed
task-issues.json    # Task ID → GitHub issue number mapping (GitHub mode only)
```

opencode uses a central JSON configuration file rather than per-agent markdown files.

---

## Agent Definition Format

Agents are defined in `opencode.json` (or `.opencode/config.json`) under the `agent` key:

```json
{
  "agent": {
    "pm-agent": {
      "description": "Planning front door. Audits source, resolves questions, and creates an approval-ready sprint without implementing it.",
      "mode": "primary",
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
    "team-lead": {
      "description": "Execution front door. Executes an approved plan through workers, gates, commits, reporting, and acceptance preparation.",
      "mode": "primary",
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
| `primary` | Human-facing front-door agent, such as `pm-agent` or `team-lead` |
| `subagent` | Delegated worker invoked by `pm-agent` or `team-lead` |

### Required front-door agents

Define both `pm-agent` and `team-lead` as named `primary` agents:

- `pm-agent` owns planning, source audits, questions, state-backend setup, sprint artifacts, and the human approval handoff. It must not implement the plan.
- `team-lead` accepts only an approved plan and owns worker delegation, deterministic gates, commits, reporting, and acceptance preparation.

Their instructions must read `AGENTS.md` and `TEAM-ORCHESTRATION.md` first, state their entry conditions, and use the canonical progress/report headings. Add always-loaded routing guidance to `AGENTS.md`:

```markdown
## Orchestration Routing

- Use the `pm-agent` primary agent for planning, decomposition, sprint creation, and "plan this" requests.
- Use the `team-lead` primary agent for approved-plan execution and "execute the plan" requests.
- Do not perform, imitate, collapse, or bypass either workflow from another primary agent.
- `pm-agent` stops at human approval; `team-lead` refuses execution without an approved plan or explicit human override.
```

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

## Native Delegation

Select `pm-agent` for planning and `team-lead` for approved-plan execution. The selected front-door agent delegates worker phases with opencode's native `@mention` syntax. For example:

```text
@test-writer Write tests for TASK-003 using the selected state backend and the approved contract.
```

Always include the task identifier, selected state backend, files to read, and expected report format. Record the worker result before starting the next gate. If the required front-door agent cannot be selected, stop instead of collapsing its workflow into another primary agent.

---

## Notes

- `pm-agent` and `team-lead` are required named primary agents; worker subagents do not replace them.
- Configure opencode's permission system (`Ask`/`Allow`/`Deny` per action) according to each agent's responsibilities.
- Keep the routing block in `AGENTS.md` explicit so it applies regardless of which primary agent starts the session.
- The `task-issues.json` file is created during brainstorming and lives at the project root.
- Model provider keys (e.g., `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`) must be set in your environment before using the configured agents.
- Follow `TEAM-ORCHESTRATION.md` as the canonical, harness-agnostic workflow; this file is only the opencode adapter for paths, formats, and native delegation.
- Do not restate or override canonical state-backend, quality-gate, commit-gate, readiness, or issue-disposition rules here.
