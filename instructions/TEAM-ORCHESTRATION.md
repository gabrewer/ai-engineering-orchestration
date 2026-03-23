# Team Orchestration

This project uses **agentloop** — a .NET CLI in `tools/agentloop/` that orchestrates a team of AI agent subprocesses as an automated build pipeline. The goal is to minimize human involvement during execution while maximizing quality and auditability.

> **Trigger phrase**: Say "execute the plan" (or similar) to run the `agentloop` CLI and begin execution.

> **Prerequisite**: Agent definitions live in `agents/`. Run via `dotnet run --project tools/agentloop` from the project root, or use the compiled binary at `loop/dist/agentloop`.

> **AI Tool Setup**: Agent definition format, directory paths, model names, and invocation commands vary by AI tool. See the relevant `TOOL-*.md` file in this `instructions/` directory for your tool's specific configuration.

---

## Philosophy

The main AI session and agentloop together act as the **Team Lead**. They own the execution plan, orchestrate all agents, manage the dependency graph, and make the escalation call: small issues get auto-fixed, big ones get flagged for the human. The system starts conservative and earns more autonomy over time as breadcrumbs prove good judgment.

---

## The Team

Every agent is defined as a file in `agents/`. The exact format (markdown with YAML frontmatter, JSON, etc.) depends on your AI tool — see the relevant `TOOL-*.md` for details. The example below shows the Claude Code format:

```markdown
---
model: sonnet
tools: Read,Write,Edit,Glob,Grep,Bash
---

Your system prompt here...
```

### `product-designer`

Expands milestones into detailed sprint briefs.

- Reads the master PRD and expands every milestone into concrete requirements
- Defines user stories, screen descriptions, interaction details, and edge cases
- Makes UX decisions — doesn't leave ambiguity for the PM
- Writes sprint briefs to `docs/sprints/<sprint-name>-brief.md`
- If ambiguity can't be resolved, writes questions to `docs/sprints/questions.md`
- **Tools**: Read, Write, Edit, Glob, Grep, Bash
- **Model**: Opus

### `pm`

Turns sprint briefs into actionable sprint plans.

- Reads sprint briefs (from Product Designer) and produces structured sprint JSON files
- Each Task is either **prescriptive** (specific implementation instructions) or **goal-oriented** (desired outcome, agent decides approach)
- Writes sprint plans to `docs/sprints/<sprint-name>-<timestamp>.json`
- If briefs have unresolved ambiguity, appends questions to `docs/sprints/questions.md`
- Also writes sprint summaries after build loop execution completes
- **Tools**: Read, Write, Glob, Grep, Bash

### `domain-modeler`

Defines the domain before anyone writes code.

- Produces the domain model: entities, aggregates, value objects, events, commands
- For event-sourced systems (like this one using Marten), defines the event catalog — the foundational contract everything else builds on
- Runs early in each Sprint before builders touch anything
- Collaborates with the PM Agent to ensure Tasks align with the domain model
- Leaves breadcrumbs for every modeling decision
- **Tools**: Read, Write, Edit, Glob, Grep, Bash

### `api-developer`

Defines and builds the contract between frontend and backend.

- Produces API specifications (endpoints, request/response shapes, error contracts)
- Both frontend and backend builders work against this contract — it prevents drift
- Runs after the Domain Modeler and before the builders
- Updates the contract when domain changes require it
- Leaves breadcrumbs for every contract decision
- **Tools**: Read, Write, Edit, Glob, Grep, Bash

### `test-writer`

Writes tests for a given task — before any implementation exists.

- Tests must fail 100% when written. If any test passes at write time, that is a hard stop
- Works against the API contract and domain model
- **Backend tasks**: xUnit **unit tests only** — pure domain logic, aggregates, events, handlers. No Docker, no Aspire stack, no HTTP calls. Integration tests are out of scope and run manually.
- **Frontend tasks**: Vitest for component logic and hooks — no browser, no real API calls
- Playwright is **not** the test-writer's responsibility — see `frontend-builder` below
- **Tools**: Read, Write, Glob, Grep, Bash (for running tests only)

### `backend-builder`

Owns the server-side application.

- Builds API endpoints, domain logic, data access, authentication, infrastructure
- Works against the API contract and domain model
- **Never alters a test** — if a test seems wrong, it flags it and stops
- Done when all task tests pass
- Leaves breadcrumbs for every architectural decision
- **Scope boundary**: when given review feedback, reads only the files explicitly named in the feedback and makes exactly the changes described. Does not explore the broader codebase or refactor adjacent code.
- **If review feedback names a file this task did not create or modify**: outputs `BLOCKED: <filename> is pre-existing code outside this task's scope` and stops. Does not make the change, does not update comments as a substitute.
- **Tools**: Read, Write, Edit, Glob, Grep, Bash

### `frontend-builder`

Owns the client-side application.

- Builds components, routes, pages, client-side state, and API client code
- Works against the API contract — never invents endpoints
- **Never alters a test** — if a test seems wrong, it flags it and stops
- Done when all task Vitest tests pass **and** Playwright E2E tests are written
- After implementation is complete, writes Playwright E2E tests in `frontend/e2e/<task-id>/`. These target the real running stack (no mocking) and are the developer's manual regression suite (`bun run test:e2e`). They are not run by the pipeline.
- Leaves breadcrumbs for every significant UI decision
- **Scope boundary**: same rules as backend-builder — only touches files this task created or modified. Outputs `BLOCKED` if asked to fix pre-existing code in other files.
- **Tools**: Read, Write, Edit, Glob, Grep, Bash

### `destroyer`

Stress-tests completed work. The adversarial half of the immune system.

- Reviews code for correctness, security, edge cases, and adherence to the domain model and API contract
- Writes adversarial tests — but **only for code this task created or modified**. Never writes tests for pre-existing code or out-of-scope behavior — those tests fail permanently and poison subsequent tasks.
- Only reports **critical** and **high** severity findings as actionable. Medium and low go in a non-blocking notes section that the review-agent cannot route to builders.
- Does NOT fix issues — reports them to the Review Agent
- Leaves breadcrumbs documenting what was tested, what survived, and what broke
- **Scope boundary**: starts with files explicitly listed in the task description. Only expands to related files if a finding requires broader context. Does not grep or glob across the entire codebase. Does not re-report issues that are clearly pre-existing in other tasks' code.
- **Most tasks should produce CLEAN or one high finding.** Quantity of findings does not equal quality — flag at most one issue per category.
- **Tools**: Read, Write, Glob, Grep, Bash (read-only commands except for writing tests)

### `review-agent`

Triages destroyer findings and drives resolution.

- Assesses each issue the Destroyer raises
- Routes issues to the appropriate builder for fixes
- Verifies fixes after builders address them
- Applies the escalation threshold: small issues (style, naming, minor refactors) get auto-resolved; big issues (architectural concerns, security, fundamental approach problems) get escalated to the human
- Leaves breadcrumbs documenting the triage decision and resolution for every issue
- **Pre-existing bugs are not this task's responsibility.** If a finding is in code not written or modified by this task, the review-agent marks it `DEFERRED` and does not route it to the builder. It ships unless the pre-existing bug actively breaks this task's own work (security issue or domain model violation). Deferred findings are noted for a future task to own.
- **Output**: Emits exactly one of:
  - `SHIP IT` — all issues resolved or acceptably low risk
  - `CHANGES NEEDED: <exact problem description>` — builder must fix specific issues (file:line references, surgical — no background context)
  - `ESCALATE: <problem description>` — requires human review
- **If CHANGES NEEDED**: agentloop spawns a new builder subprocess to address the problems. The loop repeats up to 6 times.
- **Tools**: Read, Glob, Grep, Bash (read-only commands only)

### `git-committer`

Commits all task work after the review agent approves.

- Triggered automatically by agentloop after `SHIP IT`
- **Tools**: Read, Glob, Grep, Bash

Logs for all agents are written to `.agentloop/logs/` keyed by run ID, task ID, and agent name.

---

## GitHub Process

GitHub is the **source of truth**. All state is tracked there in real time — not in batches. All `gh` CLI calls use the `gh` tool.

- Every feature has a **feature branch**
- Every feature has a **parent (epic) issue** with tasks grouped into steps
- Every task has its own **child issue**
- Every task has an **emoji status indicator** (see key below)

### Task Status Key

| Emoji | Status |
|-------|--------|
| 🏃 | doing |
| ✋ | blocked |
| 🔴 | on hold |
| 🔵 | more investigation required |
| 👀 | human review required |
| ✅ | done |

---

## Rules

- Do not say a problem is fixed unless the app can build.
- Do not say something is done unless you actually did it.
- Never run anything against prod unless explicitly told to.
- Never install packages by editing `.csproj` directly — use `dotnet add package`. Never edit `package.json` directly — use the frontend package manager CLI.

---

## Task Definition

Each Task in the Sprint plan includes:

- **Name** — short, descriptive
- **Type** — prescriptive or goal-oriented
- **Description** — what needs to be done (prescriptive: specific instructions; goal-oriented: desired outcome)
- **Acceptance criteria** — how to know it's done
- **Dependencies** — which Tasks must complete first
- **Sprint** — which Sprint it belongs to
- **Assigned to** — which builder agent owns it

---

## High-Level Flow

Two separate loops with a human review gate between them:

```
PLANNING LOOP (interactive, daytime):
  product-designer → pm → questions? → human answers → re-run
  Output: docs/sprints/<sprint>.json for each milestone

  ↓ human reviews plans ↓

BUILD LOOP (autonomous, overnight):
  [per sprint]: domain-model → api-contract → [per task]: test → build → build-gate → destroy → review → commit → smoke-test → pm summary
  refine → report
```

Each step is either **agentic** (an AI agent subprocess does it) or **deterministic** (a shell command, always the same result).

### What is deterministic

- All **git commits** — handled by the `git-committer` agent subprocess after review agent approval
- All **verification scripts** — shell scripts defined during brainstorming, invoked after commit

### What is agentic

- Domain modeling (`domain-modeler` subprocess)
- API contract definition (`api-developer` subprocess)
- Test writing (`test-writer` subprocess)
- Code generation (`backend-builder` / `frontend-builder` subprocess)
- Adversarial testing (`destroyer` subprocess)
- Issue triage and review (`review-agent` subprocess)
- Sprint summary (`pm` subprocess)
- Brainstorming and planning (interactive, with the user)
- Execution planning (via the AI tool's headless/non-interactive invocation, no tools — pure dependency reasoning)
- Refinement (interactive Q&A handoff)

---

## How agentloop Works

The `agentloop` CLI (in `tools/agentloop/`) is the execution engine. Run it from the project root:

```bash
dotnet run --project tools/agentloop -- build --prd <sprint-name>        # single sprint
dotnet run --project tools/agentloop -- build --prd <sprint-name> --resume  # resume
dotnet run --project tools/agentloop -- build --prd <sprint-name> --yes  # skip confirmation
dotnet run --project tools/agentloop -- build --all                      # all sprints
dotnet run --project tools/agentloop -- build --all --resume             # resume all
dotnet run --project tools/agentloop -- plan --prd docs/PRD.md           # planning loop
```

Or use the compiled binary directly:

```bash
loop/dist/agentloop build --prd <sprint-name>
loop/dist/agentloop build --all --resume
```

### What agentloop does

1. Reads the sprint plan from `docs/sprints/<sprint-name>.json`
2. Calls the AI tool's headless invocation command with no tools to produce a dependency graph and wave order
3. Presents the execution plan for human approval (with optional feedback loop)
4. Executes Sprints in sequence; tasks within a wave run in parallel
5. For each Sprint: `domain-modeler` → `api-developer` → per-task pipeline
6. For each task: `test-writer` → builders → `destroyer` → `review-agent` (up to 6 attempts) → `git-committer`
7. After all tasks: `pm` agent writes a sprint summary
8. Shows a live terminal dashboard throughout

---

## Phase 1: Brainstorm, Plan, Commit

This phase is **interactive** — the user and the AI work together. Use the `/brainstorming` skill.

Once the user approves the plan, the skill runs a **preflight check** before creating any artifacts:

- A local git repo exists — if not, offer to `git init`
- The current branch is not `main` or `master` — if it is, create the feature branch now (the name is known at this point)
- A remote is configured — if not, ask for the URL and offer to add it and push

> Note: The `/brainstorming` skill must be created in `skills/brainstorming.md`. See the relevant `TOOL-*.md` for the exact path your tool expects.

### Brainstorming process

1. **Explore**: Lateral thinking and deep exploration of the feature — what it is, what it affects, what could go wrong.
2. **Clarify** (3 rounds): Ask focused questions to extract detail about both the feature intent and the implementation approach. One round at a time.
3. **Propose** (3 rounds): Offer distinct solution approaches with tradeoffs. The user can steer, reject, or combine. One round at a time.
4. **Verification design**: For each task, propose specific, deterministic verification steps. Examples:
   - CSV processing: row count check, column sum validation
   - Web app: `dotnet build` exits 0, frontend `bun run build` exits 0, Playwright snapshot confirms a key element is present on the page
   - API: curl returning expected status code and response shape

### After approval

Once the user approves the plan:

- Create a **feature branch** locally
- Create a **plan document** at `/docs/plans/<feature-name>.md`
- Create an **epic issue** on GitHub with tasks grouped into second-level headers with emoji
  - Every task has its status emoji (start with 🏃 for the first task, rest unlabeled)
  - Every task has its own child issue
  - Every issue has appropriate labels applied
- Create **verification scripts** at `verify/<feature-name>/` — one shell script per task that needs verification, named by task ID (e.g., `verify/user-auth/task-003.sh`).
- Create `task-issues.json` — a mapping of task IDs to GitHub issue numbers (e.g., `{"task-001": 42, "task-002": 43}`).
- Commit everything: plan doc, verification scripts, and any other local artifacts

---

## Phase 2: Execution (agentloop CLI)

This phase is kicked off when the user says "execute the plan" or equivalent. The AI runs the `agentloop` CLI from the project root and monitors progress.

### Real-time GitHub issue title updates

The main AI session is responsible for updating issue titles at key moments — **before** launching agentloop, not after.

**When starting a task** (before launching agentloop):
```bash
# Read current title, strip any existing emoji, prepend 🏃
CURRENT=$(gh issue view <issue-number> --json title -q .title)
gh issue edit <issue-number> --title "🏃 $CURRENT"
```

**When a builder outputs `BLOCKED:`** (hard stop, task not completed):
```bash
CURRENT=$(gh issue view <issue-number> --json title -q .title)
# Strip emoji prefix first, then add ✋
CLEAN=$(echo "$CURRENT" | sed 's/^[^ ]* //')
gh issue edit <issue-number> --title "✋ $CLEAN"
gh issue comment <issue-number> --body "✋ Blocked: <reason from builder output>"
```

**When the destroyer flags an issue for escalation**:
```bash
CURRENT=$(gh issue view <issue-number> --json title -q .title)
CLEAN=$(echo "$CURRENT" | sed 's/^[^ ]* //')
gh issue edit <issue-number> --title "👀 $CLEAN"
gh issue comment <issue-number> --body "👀 Escalated: <reason from review-agent output>"
```

### The per-Sprint pipeline

#### Step 1: Domain Modeling

The `domain-modeler` runs first for each Sprint's scope. It defines or updates:
- Entities, aggregates, value objects
- Events and commands (critical for Marten event sourcing)
- The event catalog is locked before building begins

#### Step 2: API Contract

The `api-developer` defines or updates the API contract for this Sprint's tasks. Both frontend and backend builders code against this contract — it prevents drift.

#### Step 3: Per-Task Pipeline

For each task in the Sprint:

1. **`test-writer`** — writes tests that must fail 100% at write time
2. **Builders** (`backend-builder` / `frontend-builder`) — write code until all tests pass
3. **Build gate** — `dotnet build` must exit 0 before the destroyer runs. If it fails, the error is fed back to the builder. Code that does not compile never reaches the reviewer.
4. **`destroyer`** — adversarial testing scoped to this task's code only. Only critical/high findings are actionable. Medium/low are noted but do not block.
5. **`review-agent`** — triages destroyer findings, routes fixes to builders, escalates big issues
6. **`git-committer`** — commits after `SHIP IT`
7. **Failed task cleanup** — if a task exceeds max review attempts, all uncommitted working tree changes are discarded (`git checkout -- . && git clean -fd`) so broken code does not leak into subsequent tasks.

#### Step 4: Sprint Smoke Test

After all tasks complete (before the PM summary), agentloop runs a sprint-level smoke test:

1. `dotnet build <solution>` — full solution must compile clean
2. `dotnet test <solution>` — full test suite must pass

If the build fails, the sprint is flagged and the PM summary still runs (so there's a written record), but the failure is surfaced clearly. **A sprint is not considered done unless the smoke test passes.**

The script exits `0` on success or non-zero on failure. Failure stops the pipeline and updates the GitHub issue to ✋ (blocked), requiring human review.

---

## Phase 3: Refinement and Reporting

The Refinement step is a **human-in-the-loop handoff**. After execution completes, Claude guides the user through a focused Q&A review:

- Questions are based on the actual work performed
- The goal is quality, trust, and shipping — not scope expansion
- Outcomes are: ship as-is, tweak and ship, or flag for follow-up

This is not an automated step. The user decides what happens next.

A final **report** is posted as a comment on the epic GitHub issue summarizing:
- What was built
- What was verified and how
- Any decisions made or tradeoffs taken
- What to watch for in production

---

## Breadcrumb Protocol

Every agent follows the same breadcrumb format for every significant action:

- **Who** — which agent
- **What** — what was done or decided
- **Why** — the reasoning behind the choice
- **Alternatives considered** — what else was on the table and why it was rejected
- **Confidence** — how sure the agent is about this call (high/medium/low)

Low-confidence breadcrumbs are candidates for escalation. The Review Agent and Team Lead use confidence signals to calibrate the auto-fix vs. escalate threshold.

Breadcrumbs are written to the agent's log output in `.agentloop/logs/`.

---

## Trust & Autonomy Model

The system starts conservative and evolves:

**Conservative (default):**
- Strictly phased execution — no overlap between build and destroy phases
- Sequential Task execution within each phase
- Low escalation threshold — most non-trivial issues flagged for human review
- Coordinator follows the playbook exactly

**Moderate (earned):**
- Parallel Task execution within build phase for clearly independent Tasks
- Higher escalation threshold — only architectural and security concerns flagged
- Coordinator can reorder Tasks within a Sprint if dependencies allow

**Autonomous (high trust):**
- Overlapping phases — next Sprint's planning can begin while current Sprint is in review
- Builders can propose API contract changes directly (API Developer reviews)
- Destroyer findings below a severity threshold get auto-resolved without Team Lead involvement
- Coordinator can adjust Sprint scope based on what it learns during execution

Trust level is configured by the human and informed by breadcrumb review. Reading the breadcrumbs and seeing good decisions is how trust is built.

---

## Artifacts Summary

| Artifact | Location | Created by |
|----------|----------|------------|
| Master PRD | `docs/PRD.md` | Brainstorming skill |
| Sprint briefs | `docs/sprints/<sprint>-brief.md` | Product Designer (plan loop) |
| Questions | `docs/sprints/questions.md` | Product Designer / PM (plan loop) |
| Answers | `docs/sprints/answers.md` | Human |
| Sprint plans | `docs/sprints/<sprint>.json` | PM (plan loop) |
| Domain model | `docs/domain/<sprint>.md` | Domain Modeler (build loop) |
| API contract | `docs/api/<sprint>.md` | API Developer (build loop) |
| Agent definitions | `agents/` | Setup (one-time) — see `TOOL-*.md` for format |
| agentloop CLI | `tools/agentloop/` | Setup (one-time) |
| Run logs | `.agentloop/logs/` | Build loop |
| Run state | `.agentloop/state-<sprint>.json` | Build loop |
