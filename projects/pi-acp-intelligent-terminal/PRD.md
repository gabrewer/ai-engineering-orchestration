# PRD: pi-acp Intelligent Terminal Compatibility

## Product Name & One-Liner

**pi-acp Intelligent Terminal Compatibility** — Make `pi-acp` feel like a first-class ACP agent inside Microsoft Intelligent Terminal, with readable tool output, reliable model/session behavior, and setup docs comparable to Codex, Claude Code, and GitHub Copilot.

## Problem & Audience

Microsoft Intelligent Terminal can run custom ACP-compatible agents, and `pi-acp` already bridges Pi to ACP. However, the current experience appears tuned for Zed and degrades in Intelligent Terminal: tool calls show as opaque `[bash] Completed` / `[bash] Failed` entries, failed commands lack useful stderr/stdout, and assistant/tool context is hard to follow.

This is for a developer using **Pi inside Intelligent Terminal** who expects the agent pane and command-palette workflows to behave like built-in agents: readable, inspectable, resumable, and useful during real coding tasks.

## Goals

1. Make Pi usable as a custom ACP agent in Intelligent Terminal.
2. Ensure tool calls, especially `bash`, render useful information using standard ACP fields.
3. Preserve existing Zed behavior and avoid regressions.
4. Add a clear Intelligent Terminal setup and test path.
5. Produce changes suitable for a PR back to `svkozak/pi-acp` if desired.

## Core Features

### 1. Intelligent Terminal Compatibility Mode

**Priority:** must-have

Detect Microsoft Intelligent Terminal during ACP `initialize` via `clientInfo.name`, `clientInfo.title`, or a manual environment override.

Suggested override:

```bash
PI_ACP_CLIENT_COMPAT=intelligent-terminal
```

Behavior in this mode should favor standard ACP content over client-specific `_meta` rendering assumptions.

### 2. Standard Bash Tool Output Rendering

**Priority:** must-have

For `bash` tool calls, always emit human-readable ACP `tool_call_update.content` containing:

- command
- stdout/stderr or accumulated output
- exit code when known
- failure marker when command fails

Intelligent Terminal should not only show:

```text
[bash] Completed
[bash] Failed
```

It should show something closer to:

```text
$ git status --short --branch
fatal: not a git repository: (NULL)
Exit code: 128
```

### 3. Better Tool Titles

**Priority:** must-have

Use meaningful tool titles in ACP updates:

- `bash`: actual command string when available
- `read`: file path
- `edit` / `write`: affected path
- fallback: tool name

This helps Intelligent Terminal render tool activity clearly even when it collapses details.

### 4. Failed Tool Diagnostics

**Priority:** must-have

When any Pi tool fails, include standard ACP-visible diagnostic text. Do not rely only on `rawOutput` or Zed-specific metadata.

Minimum content:

- tool name
- relevant args/path/command
- error text
- exit code if applicable

### 5. Preserve Zed-Specific Enhancements

**Priority:** must-have

Do not remove existing Zed-oriented behavior such as terminal metadata, structured diffs, locations, or session history support. The change should add a compatibility layer, not replace current behavior.

### 6. Intelligent Terminal Setup Documentation

**Priority:** should-have

Add a documentation section for Microsoft Intelligent Terminal:

- install Pi
- install/fork `pi-acp`
- configure custom ACP agent command
- recommended command for Windows:

```powershell
pi-acp.cmd
```

or:

```powershell
npx.cmd -y pi-acp
```

- optional environment variables
- known limitations around custom agent session management hooks

### 7. Compatibility Test Script / Manual Test Plan

**Priority:** should-have

Add a small test plan or smoke script that verifies the ACP output shape for Intelligent Terminal-sensitive cases:

- successful bash command
- failed bash command
- read file
- edit file
- model list/config options
- cancellation
- session load/resume

Automated tests should validate emitted ACP updates include standard `content` for failed and successful bash calls.

### 8. Quiet Startup Option for Intelligent Terminal

**Priority:** nice-to-have

If Intelligent Terminal renders startup info noisily, default to quieter startup in compatibility mode while preserving update notices and critical auth/setup errors.

## Non-Goals

- Rewriting `pi-acp` from scratch.
- Replacing Pi RPC with the Pi SDK in this phase.
- Implementing ACP filesystem delegation (`fs/*`).
- Implementing ACP terminal delegation (`terminal/*`).
- Making Intelligent Terminal’s custom-agent session management panel fully support Pi if that requires Microsoft-side hooks.
- Removing Zed support or changing Zed’s current behavior intentionally.
- Adding new Pi core features unless required by adapter behavior.

## Technical Considerations

Implementation repo:

```text
https://github.com/gabrewer/pi-acp
```

Likely files to modify:

- `src/acp/agent.ts`
- `src/acp/session.ts`
- `src/acp/translate/bash.ts`
- `src/acp/translate/pi-tools.ts`
- tests under `test/`
- `README.md`

Recommended design:

1. Add a small client compatibility detector during `initialize`.
2. Store compatibility mode on `PiAcpAgent` / session creation path.
3. Pass compatibility mode into `PiAcpSession`.
4. In bash/tool translation, emit standard ACP `content` for Intelligent Terminal and probably for all clients unless it causes Zed duplication.
5. Keep existing `_meta` terminal output for Zed.

Possible compatibility modes:

```ts
type ClientCompatMode = 'default' | 'zed' | 'intelligent-terminal';
```

Detection sources:

- `PI_ACP_CLIENT_COMPAT`
- `initialize.clientInfo.name`
- `initialize.clientInfo.title`

Risk: emitting both `_meta` terminal output and normal `content` may duplicate output in Zed. If duplication occurs, gate plain content to `intelligent-terminal` mode first.

## Acceptance Criteria

### Agent Pane

- Intelligent Terminal can launch `pi-acp` as a custom ACP agent.
- The agent connects successfully.
- A simple prompt produces streamed assistant text.

### Bash Success

Prompt:

```text
Run pwd and tell me where we are.
```

Expected:

- Tool title includes `pwd` or equivalent command.
- Tool details include command output.
- Final response is readable.

### Bash Failure

Prompt:

```text
Run a command that fails, then explain the failure.
```

Expected:

- Intelligent Terminal displays useful failed command output.
- Exit code is visible when available.
- It does not only show `[bash] Failed`.

### Git Failure Case

In a non-repo directory, prompt:

```text
Review the current git diff.
```

Expected:

- Failed `git status` / `git diff` output is visible.
- Assistant can react to the actual failure.

### File Tools

Prompt:

```text
Read README.md and summarize it.
```

Expected:

- Tool call title references `README.md`.
- Tool status is readable.

### No Zed Regression

Existing smoke tests pass.

```bash
npm test
npm run build
```

If possible, manually verify Zed still renders terminal output/diffs acceptably.

## Milestones

### Milestone 1 — Baseline Reproduction

Deliverable: notes or screenshots showing current Intelligent Terminal behavior with `gabrewer/pi-acp`.

Tasks:

- Configure Intelligent Terminal custom ACP agent.
- Capture successful prompt output.
- Capture failed bash output.
- Capture tool rendering issue.

### Milestone 2 — Compatibility Mode

Deliverable: code path that detects or forces Intelligent Terminal mode.

Tasks:

- Add `ClientCompatMode` helper.
- Read `PI_ACP_CLIENT_COMPAT`.
- Detect client info from `initialize`.
- Thread mode into sessions.

### Milestone 3 — Standard Tool Content

Deliverable: Intelligent Terminal displays useful bash/tool output.

Tasks:

- Update bash translation to produce plain ACP content.
- Include command, output, and exit code.
- Ensure failed commands expose diagnostics.
- Add tests for emitted updates.

### Milestone 4 — Documentation and Test Plan

Deliverable: README section and manual test checklist.

Tasks:

- Add Intelligent Terminal setup docs.
- Add known limitations.
- Add manual verification checklist.

### Milestone 5 — Polish / Upstream PR Prep

Deliverable: clean branch ready for review.

Tasks:

- Run build/tests.
- Confirm no Zed regression where possible.
- Prepare PR summary with before/after examples.

## Open Questions

1. What exact `clientInfo` does Microsoft Intelligent Terminal send during ACP `initialize`?
2. Does Intelligent Terminal render `tool_call_update.content`, `rawOutput`, both, or only selected fields?
3. Does emitting plain `content` duplicate output in Zed?
4. Should plain bash content be emitted for all clients or only Intelligent Terminal mode?
5. Can Intelligent Terminal custom agents participate in session management, or is that blocked by current built-in hook limitations?
6. Should this remain a fork-only improvement or be proposed upstream to `svkozak/pi-acp`?
