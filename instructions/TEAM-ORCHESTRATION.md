# Team Orchestration

This project uses a team of specialized AI roles as a structured planning and build workflow. The active AI session is the coordinator: it loads or delegates to the appropriate worker instructions, enforces quality gates, and records progress in the selected state backend.

> **Trigger phrase**: Say "execute the plan" (or similar) to start the tool's team-lead workflow.

> **AI Tool Setup**: Agent definition format, directory paths, model names, and delegation capabilities vary by AI tool. See the relevant `TOOL-*.md` file in this `instructions/` directory for your tool's specific configuration.

---

## Philosophy

The main AI session acts as the **Team Lead**. It owns the execution plan, coordinates worker roles, manages the dependency graph, and makes the escalation call: small issues get auto-fixed, big ones get flagged for the human. The system starts conservative and earns more autonomy over time as breadcrumbs prove good judgment.

## Workflow Entry Points

Every tool adapter must present two cohesive front doors:

- `pm-agent` coordinates product design, PM work, questions, approval, and planning artifacts;
- `team-lead` coordinates approved-plan execution, workers, quality gates, commits, reporting, and acceptance preparation.

Use each tool's native representation: Pi prompt templates, Claude Code and GitHub Copilot agent files, and opencode `primary` agents. Internal phases belong in worker skills or subagents rather than requiring the user to understand the worker graph. Additional entry points are useful only when they provide a genuinely separate workflow or focused utility; their ownership must not overlap or leave gaps between planning and execution.

When a tool supports native agents, the primary/default session must route planning requests to `pm-agent` and execution requests to `team-lead`. It must not imitate, collapse, or bypass these front-door agents. Each adapter must put this routing rule in the tool's always-loaded project instructions as well as defining the agents themselves.

When generating orchestration resources, first show a compact map of proposed front-door agents, worker skills/subagents, and model assignments. A quick sanity check is: can a user plan and execute work through the front doors without knowing which internal worker runs each phase?

---

## The Team

Every front-door agent and worker role is defined using the active AI tool's native prompts, skills, instructions, or agent files. The exact format depends on the tool — see the relevant `TOOL-*.md` for details. The example below shows the Claude Code format:

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
- Writes durable sprint briefs to `docs/sprints/<sprint-name>-brief.md` when the brief is a product/design deliverable; otherwise records planning output in the selected state backend
- If ambiguity can't be resolved, posts questions to the selected state backend using the 🧭 planning status
- **Tools**: Read, Write, Edit, Glob, Grep, Bash
- **Model**: Opus

### `pm`

Turns sprint briefs into actionable sprint plans.

- Reads sprint briefs (from Product Designer) and produces structured sprint plans
- Each Task is either **prescriptive** (specific implementation instructions) or **goal-oriented** (desired outcome, agent decides approach)
- Writes machine-readable sprint plans only when the selected workflow needs them; execution state lives in the selected backend
- Creates or updates the selected state backend with the human-readable task board, Contract Impact Check, dependencies, and Quality Gates
- If briefs have unresolved ambiguity, records the specific questions in the selected state backend and marks the sprint blocked/needs-input
- Posts sprint summaries to the selected state backend after build loop execution completes
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

- Tests for genuinely new behavior should fail before implementation. Regression tests for existing guarantees may already pass; record those as baseline/resilient evidence. If a supposed new-behavior test passes, inspect whether the behavior already exists or the test is too weak before continuing.
- Works against the API contract and domain model
- **Backend tasks**: follow the repository's documented test frameworks and conventions; do not impose a unit-test-only policy. Use unit tests for isolated pure logic when valuable, and automated integration tests whenever correctness depends on HTTP contracts, authentication, authorization, ownership/tenancy, persistence, transactions, concurrency, messaging, service discovery, gateway/routing policy, or other runtime boundaries. Prefer real project-owned dependencies; mock or emulate external providers only as the repository permits.
- Security-sensitive boundary tests must exercise rejection paths such as unauthenticated, unauthorized, cross-user/tenant, forged or nonexistent linkage, failed authority/provider dependencies, no-mutation-on-rejection, and genuine concurrent attempts where applicable.
- Integration evidence belongs in the automated task/sprint verification path rather than being deferred to an unspecified manual run. If required infrastructure cannot run, report the check as `NOT CHECKED` with the reason and treat it as blocking unless the human explicitly accepts the gap.
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
- Does NOT fix issues — reports them to the Review Agent via `## 🔥 Destroy Report: ...` in the selected state backend
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
- Posts `## 👀 Review Report: ...` reports to the selected state backend and leaves breadcrumbs documenting the triage decision and resolution for every issue
- **Pre-existing bugs are not this task's responsibility.** If a finding is in code not written or modified by this task, the review-agent marks it `DEFERRED` and does not route it to the builder. It ships unless the pre-existing bug actively breaks this task's own work (security issue or domain model violation). Deferred findings are noted for a future task to own.
- **Output**: Emits exactly one of:
  - `SHIP IT` — all issues resolved or acceptably low risk
  - `CHANGES NEEDED: <exact problem description>` — builder must fix specific issues (file:line references, surgical — no background context)
  - `ESCALATE: <problem description>` — requires human review
- **If CHANGES NEEDED**: the Team Lead delegates the specific remediation to the appropriate builder role. The loop repeats up to 6 times.
- **Tools**: Read, Glob, Grep, Bash (read-only commands only)

### `git-committer`

Commits all task work after the review agent approves.

- Triggered by the Team Lead after `SHIP IT`
- **Tools**: Read, Glob, Grep, Bash

If the active AI tool produces local session or worker logs, treat them as untracked diagnostic traces. The selected state backend remains the durable source of truth for sprint/task state.

---

## State Tracking Backend

The durable state backend is a **repository-level setup choice**, not a per-plan choice. Ask the user once while installing or initializing the repo's agents:

1. **GitHub Issues mode** — use for GitHub-backed planning/tracking, issue comments, and remote team auditability.
2. **Filesystem mode** — use for local markdown/JSON plans, offline/private tracking, or no GitHub dependency.

Do **not** choose or infer the backend autonomously. Persist the answer in the repo-level agent instruction file (`AGENTS.md` or the harness-equivalent committed context file) using exactly one of these markers:

```markdown
**State backend:** github-issues
```

```markdown
**State backend:** filesystem
```

Planning, execution, skills, and subprocesses must reuse that persisted setting from the repository instructions automatically loaded by the harness. They must not ask the user to choose again when the marker exists or spend a separate tool call re-reading `AGENTS.md` when its content is already in context. Every generated agent definition, worker skill, and human-facing prompt (including PM and team-lead prompts) must carry a short resolution rule; do not copy the selected value into every agent or rely on only the top-level coordinator knowing it. If a worker receives no backend argument, it uses the repository context instead of asking the user.

If an already-initialized repository has no marker, treat that as incomplete setup: ask once, persist the answer before creating planning artifacts, and use it thereafter. A user may explicitly request a repository-wide backend change; update the marker, but do not move an active sprint between backends without a separate migration plan.

The configured backend is the **source of truth** for execution state. All sprint/task progress, agent updates, adversarial findings, review verdicts, test reports, decisions, and completion summaries are tracked there in real time — not in batches. Copy the configured marker into each sprint file or epic issue as a self-contained record; this is documentation, not another choice prompt.

### GitHub Issues Mode

- All `gh` CLI calls use the `gh` tool.
- Every feature has a **feature branch**.
- Every feature has a **parent (epic) issue** with tasks grouped into steps.
- Every task has its own **child issue**, unless the project intentionally uses a single sprint issue with an embedded checklist.
- Every task has an **emoji status indicator** (see key below).
- Routine progress artifacts are **GitHub issue comments**, not new files under `docs/sprints/`, `docs/reviews/`, or `docs/reports/`.
- Durable product, architecture, migration, or API documentation may still live under `docs/` when it is a real deliverable rather than sprint status.
- **Never close GitHub issues. Never apply final completion/disposition labels such as `done`, `complete`, or `shipped`.** Agents may only post final summary / ready-for-human-disposition comments and update non-final progress markers in the issue body/title when requested by the workflow.

### Filesystem Mode

Use repo-local markdown/JSON files as the durable state backend:

```text
docs/sprints/<sprint-id>.md          # sprint plan, task board, decisions, quality gates
docs/reviews/<sprint-id>-r<N>.md     # reviewer reports
docs/reviews/<sprint-id>-destroy-r<N>.md
docs/reports/<sprint-id>-test-r<N>.md
docs/sprints/<sprint-id>-build.md    # running agent updates / completion summary
```

In filesystem mode, agents append progress to the sprint build log and write quality-gate reports to the paths above. Do not also mirror every update into GitHub Issues unless the human explicitly asks for dual tracking.

### Sprint/Epic Structure

Use this structure for a GitHub epic/sprint issue or a filesystem sprint markdown file so any agent can resume without local context:

```markdown
## 🧭 Sprint: <sprint-or-feature-id>

**Status:** 🧭 planning | 🧱 ready | 🚧 in progress | 👀 review | 🧪 testing | ✅ done | ❌ blocked
**Goal:** <one paragraph>
**Owner / lead:** team-lead
**Design spec(s):** <paths/links or n/a>
**Related PR(s):** <links or n/a>

## 🎯 Scope

### In scope
- ...

### Out of scope
- ...

## 🔎 Contract Impact Check
- UI only? yes/no
- Existing typed API contract sufficient? yes/no with file paths
- New request/response fields needed? yes/no
- Server-side validation/auth/ownership needed? yes/no
- Cross-entity IDs or durable linkage introduced? yes/no, with write-side validation plan
- Persistence/metadata needed? yes/no
- Backend/API tests needed? yes/no
- Runtime/browser validation needed? yes/no

## 🧩 Task Board
- [ ] 🧱 **TASK-001: <title>** — `<agent>` — blocked by: none
  - **Description:** ...
  - **Files to read:** ...
  - **Acceptance:** ...
  - **Verification:** `...`
  - **Commit hint:** `...`

## 👀 Quality Gates
- [ ] 🔥 Destroyer round 1 complete
- [ ] 👀 Reviewer round 1 PASS
- [ ] 🧪 Tester/smoke round 1 PASS

## 🔗 Durable docs / artifacts
- ...

## 🧾 Decision log
- <date> — <decision> — <reason>
```

### Task Status Key

| Emoji | Status |
|-------|--------|
| 🧭 | planning / contract analysis |
| 🧱 | ready / unblocked |
| 🏃 / 🚧 | doing |
| ✋ / ❌ | blocked or failed gate |
| 🔴 | on hold |
| 🔵 | more investigation required |
| 👀 | review or human review required |
| 🧪 | testing / verification |
| 🔥 | adversarial testing / destroyer |
| 🧯 | remediation |
| ✅ | done / pass |
| 🚀 | final summary posted / ready for human disposition |
| 💤 | deferred |

### Agent Progress Protocol

Agents write stable, searchable updates to the selected state backend.

- **GitHub Issues mode:** post comments to the relevant task/epic issue. Compose long comments in the tool adapter's designated temporary directory, then post them with `gh issue comment <issue> --body-file <file>`. Never commit these temporary files.
- **Filesystem mode:** append the same markdown blocks to `docs/sprints/<sprint-id>-build.md`. Write destroy/review/test reports to the paths listed in Filesystem Mode.

Use this format for task progress in either backend:

```markdown
## <emoji> Agent Update: <agent-name> — <task-id> — Round <N>

**Status:** 🧭 planning | 🧱 ready | 🚧 in progress | ✅ completed | ❌ blocked | ⚠️ warning
**Commit(s):** <sha/link or n/a before commit gate only; completed implementation work must cite real SHA(s)>
**Summary:** <what changed or was decided>
**Verification:** <commands/results or n/a>
**Findings:** <blockers/warnings/notes or n/a>
**Next:** <next owner/action>
```

Use these quality-gate headings exactly:

- `## 🔥 Destroy Report: <sprint-or-task-id> Round <N>`
- `## 👀 Review Report: <sprint-or-task-id> Round <N>`
- `## 🧪 Test Report: <sprint-or-task-id> Round <N>`
- `## 🚀 Sprint Complete: <sprint-or-feature-id>`
- `## 🧑‍⚖️ Ready for Acceptance Verification: <sprint-or-feature-id>`

### Quality Gates Are Not Task-Board Work

Destroyer, review-agent, git-committer, and final tester/smoke phases are mandatory orchestration phases, not ordinary build tasks. Do not duplicate them as child issues or task-board checklist items unless a project explicitly needs a custom test-harness build task. Track them in a `Quality Gates` section of the parent issue/sprint file and via the standard reports above.

### Commit Gate

The team-lead must run the `git-committer` phase after the review-agent returns `SHIP IT` and before posting `## 🧑‍⚖️ Ready for Acceptance Verification` or `## 🚀 Sprint Complete`.

- Completed implementation work must cite real commit SHA(s). Do not use `Commit(s): n/a` for completed code, tests, configuration, documentation deliverables, or build fixes unless the human explicitly approved a no-commit deviation.
- If task-owned changes remain uncommitted, the sprint is not ready for acceptance verification.
- The git-committer must separate unrelated pre-existing working-tree changes from task-owned changes and must not commit tool-specific temporary files, logs, session state, or other runtime artifacts.
- If the repository is on `main` or `master`, create/use a feature branch before committing, following the project git-safety rules.
- Final readiness must include commit SHA(s), verification evidence, and any explicit no-commit deviations.

### Pull Request Size Checkpoint

Large branches hide security, integration, and review failures. Unless a repository explicitly defines different thresholds in its own instructions, every tool adapter, team-lead prompt, PM/planner prompt, and git-committer must apply these default checkpoints.

Measure branch growth against the intended pull-request base:

1. If the branch already has a pull request, use its `baseRefName` from the hosting provider.
2. Otherwise use the repository mainline branch (`origin/main`, or `origin/master` where applicable).
3. Count commits reachable from `HEAD` but not the base and count unique changed files from the merge base to `HEAD`.

**Advisory checkpoint — 8 commits or 30 changed files:** finish the current coherent task or safe batch, then recommend opening a pull request. If a pull request already exists, recommend stopping scope growth and moving it through review.

**Strong checkpoint — 15 commits or 60 changed files:** do not begin additional feature scope. Stabilize the smallest coherent change, report the branch/base/counts and existing PR URL/state, and require a human decision before more independent work is added. Move remaining independently deliverable work to a follow-up branch, sprint, or stacked pull request.

Checkpoint behavior:

- Check before implementation, after each coherent task/commit batch, and before another sprint or materially distinct concern starts on the branch.
- The PM/planner should define earlier review boundaries when a planned sprint is likely to cross a checkpoint.
- The git-committer reports `BELOW`, `ADVISORY`, or `STRONG` with commit/file counts after each successful commit. GitHub/provider metadata lookup is best effort and must not invalidate a successful commit.
- Generated files may be reported separately but still count toward review burden.
- Do not interrupt an atomic safety fix, leave a migration half-complete, or propose review while known blockers or required tests are failing. Stabilize first, then stop scope growth.
- Security boundaries, migrations, deployment changes, and public contracts should receive earlier review boundaries when independently deliverable.
- A checkpoint is a recommendation and scope-control pause, not permission to push or create a pull request without user authorization.
- If the user explicitly continues past a checkpoint, record the decision in the selected state backend and repeat the check after the next coherent batch.

### Lesson learned: high-quality sprint control issue

For large parity, migration, or multi-workstream features, prefer a single umbrella/control issue when the human wants cohesive execution instead of issue sprawl. The control issue should contain or link all of the following before implementation starts:

1. **Source delta audit** — a matrix comparing reference behavior to current behavior with exact source paths/line references, status (`implemented`, `gap`, `accepted deviation`, `blocked`), and required fix.
2. **Implementation-ready workstreams** — grouped batches with files to keep open, backend contract tasks, frontend tasks, test tasks, and final verification commands.
3. **Contract Impact Check** — full-stack by default; typed API/backend/persistence/auth/test work appears before frontend wiring whenever production behavior changes.
4. **Decision gate** — explicit human/product decisions for intentional deviations, extensions, or deferrals before coding begins.
5. **Quality gate comments** — destroyer, reviewer, and tester reports posted as comments with round numbers, blockers/warnings, and remediation evidence.
6. **Final matrix** — every audit row resolved as implemented, accepted deviation, or blocked, with source evidence and test/browser/runtime evidence.

Do not report completion from the team-lead until the final control issue/file has real commit SHA(s), verification commands/results, quality-gate verdicts, accepted deviations, unresolved risks, and a `Ready for Acceptance Verification` comment/checklist.

---

## Rules

- Do not say a problem is fixed unless the app can build.
- Do not say something is done unless you actually did it.
- Never run anything against prod unless explicitly told to.
- Never install packages by editing `.csproj` directly — use `dotnet add package`. Never edit `package.json` directly — use the frontend package manager CLI.

---

## Task Definition

Each Task in the Sprint plan or selected-backend task board includes:

- **Name** — short, descriptive
- **Type** — prescriptive or goal-oriented
- **Description** — what needs to be done (prescriptive: specific instructions; goal-oriented: desired outcome)
- **Files to read** — exact source, test, and documentation paths the agent must inspect before coding
- **Acceptance criteria** — how to know it's done
- **Verification** — exact deterministic commands to run
- **Dependencies** — which Tasks must complete first
- **Sprint** — which Sprint it belongs to
- **Assigned to** — which builder agent owns it
- **Commit hint** — conventional commit message for the smallest coherent change
- **PR slice/checkpoint** — the intended review boundary when the sprint may approach the default commit/file thresholds

Plans should reference build, test, and verification paths that actually exist. In a greenfield workstream, make establishing the first real build surface an explicit task before generating downstream scripts or plans that depend on it.

### Contract Impact Check

Every product sprint starts with a Contract Impact Check in the parent issue or sprint file. Treat user-visible workflow changes as full-stack by default unless explicitly marked `UI polish only`, `docs only`, or `frontend prototype only`.

The check answers:

- UI only? yes/no
- Existing typed API contract sufficient? yes/no, with file paths
- New request/response fields needed? yes/no
- Server-side validation/auth/ownership needed? yes/no
- Cross-entity IDs or durable linkage introduced? yes/no, with write-side validation plan
- Persistence/metadata needed? yes/no
- Backend/API tests needed? yes/no
- Runtime/browser validation needed? yes/no

If any backend/API/persistence answer is `yes`, the plan must include backend/API/test work before frontend wiring. Do not make production behavior work by tunneling structured state through free-text fields such as `notes`, `description`, or `metadataJson` when a typed contract is required.

When cross-entity IDs or durable links are introduced, write-side validation must prove create/update endpoints reject malformed IDs, nonexistent resources, deleted resources, cross-user/tenant resources, and invalid child-item references before saving. Read-side filtering or happy-path persistence alone is not sufficient evidence.

---

## High-Level Flow

Two separate loops with a human review gate between them:

```
PLANNING LOOP (interactive, daytime):
  product-designer → pm → questions? → human answers → re-run
  Output: configured repository state backend (GitHub issues or docs/sprints files) + optional docs/sprints/<sprint>.json machine plan

  ↓ human reviews plans ↓

BUILD LOOP (autonomous, overnight):
  [per sprint]: domain-model → api-contract → [per task]: test → build → build-gate → destroy → review → commit → smoke-test → pm summary
  refine → report
```

Each step is either **agentic** (the Team Lead performs it under a worker role or delegates it through the active tool) or **deterministic** (a shell command, always the same result).

### What is deterministic

- All **git commits** — handled under the `git-committer` role after review-agent approval
- All **verification scripts** — shell scripts defined during brainstorming, invoked after commit

### What is agentic

- Domain modeling (`domain-modeler` role)
- API contract definition (`api-developer` role)
- Test writing (`test-writer` role)
- Code generation (`backend-builder` / `frontend-builder` roles)
- Adversarial testing (`destroyer` role)
- Issue triage and review (`review-agent` role)
- Sprint summary (`pm` role)
- Brainstorming and planning (interactive, with the user)
- Execution planning (performed by the Team Lead from the approved plan and dependency graph)
- Refinement (interactive Q&A handoff)

---

## How Execution Works

Enter through the active AI tool's `team-lead` prompt or agent. The Team Lead:

1. Reads the approved sprint issue/file from the selected state backend.
2. Builds a dependency graph and proposes the execution order for human approval when required.
3. Executes sprints in sequence and may delegate independent tasks concurrently only when the tool supports safe isolation.
4. Runs `domain-modeler` → `api-developer` → the per-task pipeline for each sprint.
5. Runs `test-writer` → builder → build gate → `destroyer` → `review-agent` (up to 6 attempts) → `git-committer` for each task.
6. Applies the Pull Request Size Checkpoint after each committed task or coherent batch.
7. Runs sprint verification and has the `pm` role write the completion summary.
8. Records every durable status transition and report in the selected state backend.

The tool adapter may implement a role as a native subagent, a loaded skill, or a temporary role adopted by the main session. The quality gates and evidence requirements are the same in every case.

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
- Read the persisted **state backend** from the repo-level agent instructions. Do not prompt again when it is configured; if the marker is absent, complete the one-time repository setup and persist it before proceeding.
- Create a **plan document** at `/docs/plans/<feature-name>.md` only if the plan is a durable deliverable.
- Create the authoritative sprint/epic record in the configured backend:
  - **GitHub Issues mode:** create an epic issue with tasks grouped into second-level headers with emoji.
  - **Filesystem mode:** create `docs/sprints/<sprint-id>.md` using the same structure.
  - Include a Contract Impact Check before the task board.
  - Include a `Quality Gates` section for destroyer, review, and test/smoke gates.
  - Every task has its status emoji (start with 🏃/🚧 for the first task, rest 🧱 ready).
  - In GitHub mode, every task has its own child issue unless the project intentionally uses one sprint issue with embedded checklist tasks.
  - In GitHub mode, every issue has appropriate labels applied.
- Define explicit PR/review boundaries when the plan is likely to reach 8 commits or 30 changed files; split the plan into follow-up or stacked PR slices when it is likely to reach 15 commits or 60 files unless the work is genuinely atomic.
- Create **verification scripts** at `verify/<feature-name>/` — one shell script per task that needs verification, named by task ID (e.g., `verify/user-auth/task-003.sh`).
- In GitHub mode, create `task-issues.json` — a mapping of task IDs to GitHub issue numbers (e.g., `{"task-001": 42, "task-002": 43}`). In filesystem mode, omit it or map task IDs to sprint-file anchors.
- Commit durable artifacts only: plan docs that should survive, filesystem sprint files, verification scripts, task mapping, and configuration. Do not commit temporary issue-body/comment files.

---

## Phase 2: Team-Lead Execution

This phase starts when the user says "execute the plan" or invokes the tool's team-lead entry point. The active AI session reads the approved plan, coordinates the worker roles, runs deterministic gates, and records progress.

### Real-time status updates

The Team Lead updates task status in the selected backend at each key transition, before starting the corresponding worker phase.

**GitHub Issues mode** updates issue titles/comments:

```bash
# When starting a task: read current title, strip any existing emoji, prepend 🏃
CURRENT=$(gh issue view <issue-number> --json title -q .title)
gh issue edit <issue-number> --title "🏃 $CURRENT"

# When blocked: strip emoji prefix first, then add ✋ and comment
CURRENT=$(gh issue view <issue-number> --json title -q .title)
CLEAN=$(echo "$CURRENT" | sed 's/^[^ ]* //')
gh issue edit <issue-number> --title "✋ $CLEAN"
gh issue comment <issue-number> --body "✋ Blocked: <reason from builder output>"
```

**Filesystem mode** updates the sprint file/checklist and appends an agent update to the build log:

```markdown
- [ ] ✋ **TASK-003: <title>** — `<agent>` — blocked by: <reason>
```

Append details to `docs/sprints/<sprint-id>-build.md` using the Agent Progress Protocol.

When the destroyer or review-agent escalates, mark the task/gate `👀` in the selected backend and record the reason using the standard report/comment format.

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

1. **`test-writer`** — writes the repository-appropriate unit and/or integration tests required by the task's contract and risk; new-behavior tests must fail at write time, while regression tests for existing guarantees may already pass and should be recorded as resilient evidence
2. **Builders** (`backend-builder` / `frontend-builder`) — write code until all tests pass
3. **Build gate** — the repository's documented build command must exit 0 before the destroyer runs. If it fails, the error is fed back to the builder. Code that does not compile never reaches the reviewer.
4. **`destroyer`** — adversarial testing scoped to this task's code only. Only critical/high findings are actionable. Medium/low are noted but do not block.
5. **`review-agent`** — triages destroyer findings, routes fixes to builders, escalates big issues
6. **`git-committer`** — commits after `SHIP IT`, then measures and reports the Pull Request Size Checkpoint
7. **Branch growth gate** — at the strong checkpoint, do not start another independent feature task without a human decision; finish only the smallest coherent stabilization required for a reviewable branch
8. **Failed task cleanup** — if a task exceeds max review attempts, all uncommitted working tree changes are discarded (`git checkout -- . && git clean -fd`) so broken code does not leak into subsequent tasks.

#### Step 4: Sprint Smoke Test

After all tasks complete (before the PM summary), the Team Lead runs a sprint-level smoke test:

1. Run the repository's documented full build command — the complete build must pass.
2. Run the repository's documented full test command — the complete automated test suite must pass.

If the build fails, the sprint is flagged and the PM summary still runs (so there's a written record), but the failure is surfaced clearly. **A sprint is not considered done unless the smoke test passes.**

The script exits `0` on success or non-zero on failure. Failure stops the pipeline and updates the selected state backend to ✋/❌ (blocked), requiring human review.

---

## Phase 3: Refinement and Reporting

The Refinement step is a **human-in-the-loop handoff**. After execution completes, the active AI session guides the user through a focused Q&A review:

- Questions are based on the actual work performed
- The goal is quality, trust, and shipping — not scope expansion
- Outcomes are: ship as-is, tweak and ship, or flag for follow-up

This is not an automated step. The user decides what happens next.

A final **report** is recorded in the selected state backend using `## 🚀 Sprint Complete: <sprint-or-feature-id>` and summarizing:
- What was built
- What was verified and how
- Commit/PR links
- Any decisions made or tradeoffs taken
- Open warnings, deferred work, or accepted risks
- What to watch for in production

The team-lead must also post `## 🧑‍⚖️ Ready for Acceptance Verification: <sprint-or-feature-id>`. This comment is mandatory and must be derived from the **original** acceptance criteria, scope, design spec, source-of-truth, or source delta audit — not from what happened to be implemented. It must include:
- an acceptance checklist mapped to the original criteria/scope;
- manual verification steps for the human;
- expected results;
- source references, screenshots, planner/reference pages, or artifacts to inspect;
- unresolved risks, accepted deviations, and remaining deltas;
- an explicit note that tests/commits are implementation evidence only and are not acceptance.

The feature is not ready for human disposition until task-owned changes are committed and both the final completion record and the Ready for Acceptance Verification comment exist. In GitHub mode, the issue must remain open and un-final-labeled; a human verifies acceptance criteria and decides whether/when to close or label the issue. In filesystem mode, the sprint file status may be `✅ done` and the completion report plus acceptance-verification checklist must be present.

---

## Breadcrumb Protocol

Every agent follows the same breadcrumb format for every significant action:

- **Who** — which agent
- **What** — what was done or decided
- **Why** — the reasoning behind the choice
- **Alternatives considered** — what else was on the table and why it was rejected
- **Confidence** — how sure the agent is about this call (high/medium/low)

Low-confidence breadcrumbs are candidates for escalation. The Review Agent and Team Lead use confidence signals to calibrate the auto-fix vs. escalate threshold.

Breadcrumbs are written to the selected state backend. Tool-generated session logs may contain additional diagnostics, but they are not the durable record.

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
| Sprint briefs | `docs/sprints/<sprint>-brief.md`, GitHub issue body, or sprint file | Product Designer (plan loop) |
| Questions | Selected state backend; optionally `docs/sprints/questions.md` for durable planning docs | Product Designer / PM (plan loop) |
| Answers | Selected state backend; optionally `docs/sprints/answers.md` | Human |
| Sprint plans | Selected state backend; optional `docs/sprints/<sprint>.json` when the workflow needs machine-readable input | PM (plan loop) |
| Execution state | GitHub issue body/comments or `docs/sprints/<sprint-id>.md` + build log | Team Lead + all agents |
| Destroy/review/test reports | GitHub issue comments or `docs/reviews/` / `docs/reports/` files | Destroyer / Review Agent / Tester |
| Temporary issue bodies/comments | Tool-specific temp directory, untracked | Team Lead + agents |
| Domain model | `docs/domain/<sprint>.md` when durable architecture output is required | Domain Modeler (build loop) |
| API contract | `docs/api/<sprint>.md` when durable contract docs are required | API Developer (build loop) |
| Front-door and worker definitions | Tool-specific prompts, skills, instructions, or agent files | Setup (one-time) — see `TOOL-*.md` for format |
| Diagnostic logs/session state | Tool-specific runtime location, untracked | Active AI tool |
