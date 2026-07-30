# Team Orchestration

This file defines how a generated project-specific team plans and executes work. Agent discovery and generation are defined separately in [`AGENT-GENERATION.md`](AGENT-GENERATION.md). The active AI session coordinates the generated workers, enforces quality gates, and records progress in the selected state backend.

> **Trigger phrase**: Say "execute the plan" (or similar) to start the tool's team-lead workflow.

> **AI Tool Setup**: Agent definition format, directory paths, model names, and delegation capabilities vary by AI tool. See the relevant `TOOL-*.md` file in this `instructions/` directory for your tool's specific configuration.

---

## Project-Specific Agent Generation

This document is both an operating model and a generation contract. It is used to create the agents for the current project, not to define one universal team that is blindly reused across repositories.

### Source inputs

Use whichever inputs exist, in this order of authority for the relevant concern:

1. Explicit human requirements and decisions
2. The approved PRD, sprint brief, or other planning documentation
3. Repository instructions and documented project conventions
4. The current repository's source code, tests, configuration, and deployment files
5. This file and the canonical worker contracts for workflow behavior

When a PRD or other documentation describes a target environment, treat it as an intended environment and verify it against the repository. When documentation is absent or incomplete, infer implementation context from the repository and clearly record assumptions.

### Tailoring requirements

Before generating orchestration resources, identify and encode at least:

- languages, frameworks, package managers, build/test/lint commands, and repository layout;
- runtime, hosting, deployment, operating-system, browser, cloud, database, and integration environments;
- project-specific domain terminology, architecture boundaries, security/privacy risks, and coding conventions;
- applicable specialist roles, worker routing, model assignments, tool permissions, quality gates, and verification commands.

Generated agents must name the project-specific files and commands they should use, must not assume technologies or environments that are not present or approved, and must retain the role's scope boundary and handoff. If an environment is materially ambiguous, pause generation and ask the human.

### Generation output

The setup process should first show a compact map of the proposed front-door agents, specialized workers, environment tailoring, model assignments, and permissions. It then generates the active tool's native definitions and the repository-level instructions needed to route planning through `pm-agent` and execution through `team-lead`. The generated resources are project configuration; this file remains the shared source of truth for the workflow.

## Philosophy

Once execution is delegated through the `team-lead` front door, that session acts as the coordinator. It owns the execution plan, coordinates worker roles, manages the dependency graph, and makes the escalation call: small issues get auto-fixed, big ones get flagged for the human. The system starts conservative and earns more autonomy over time as breadcrumbs prove good judgment.

## Workflow Entry Points

Every tool adapter must present two cohesive delivery front doors and may present the canonical standalone PR front door:

- `pm-agent` coordinates product design, PM work, questions, approval, and planning artifacts;
- `team-lead` coordinates approved-plan execution, workers, quality gates, commits, reporting, and acceptance preparation;
- `pr-agent`, when installed, is invoked separately to prepare/open/refresh pull requests, perform cumulative base-to-head review, and report human merge readiness.

Use each tool's native representation: Pi prompt templates, Claude Code and GitHub Copilot agent files, and opencode `primary` agents. Internal phases belong in worker skills or subagents rather than requiring the user to understand the worker graph. PR Agent must never run automatically as part of Team Lead, and Team Lead completion must not imply permission to push, create, edit, or merge a pull request. Additional entry points are useful only when they provide a genuinely separate workflow or focused utility; their ownership must not overlap or leave gaps between planning, execution, and cumulative review.

When a tool supports native agents, the primary/default session must route planning requests to `pm-agent` and execution requests to `team-lead`. It must not imitate, collapse, or bypass these front-door agents. Each adapter must put this routing rule in the tool's always-loaded project instructions as well as defining the agents themselves.

A quick orchestration sanity check is: can a user plan and execute work through the front doors without knowing which internal worker runs each phase?

### Planning Authority and Execution Authority

The approved sprint manifest is the handoff contract between `pm-agent` and `team-lead`.

`pm-agent` owns product intent, scope, exclusions, task decomposition, dependencies, acceptance criteria, and planning classifications about which standards, specialist phases, quality evidence, and runtime checks apply. It records those decisions and their rationale in the authoritative sprint issue or file before approval. Planning classifications may specialize project-specific applicability, but they cannot waive gates that this workflow or the repository explicitly marks mandatory.

`team-lead` consumes those approved decisions. Its preflight confirms that the manifest is approved, complete enough to execute, internally coherent, and compatible with the current repository state. It must not repeat the PM analysis, silently broaden scope, or add speculative tasks, specialists, checks, or evidence requirements merely because they exist in a generic workflow template.

During execution, `team-lead` owns dependency ordering, worker routing, tactical implementation decisions within task scope, deterministic gates, remediation, commits, status reporting, and acceptance preparation. It may select a narrower risk-based verification command when the manifest permits it, but it may not weaken explicit acceptance criteria or required gates.

Reopen planning only when execution reveals one of these conditions:

- implementation would materially change approved behavior, scope, contracts, persistence, ownership, or operational boundaries;
- the manifest is missing or contradicts information required to make a safe implementation decision;
- the current repository state invalidates a material planning assumption;
- a repository rule or mandatory safety requirement conflicts with the approved plan.

When this happens, pause only the affected work, record the evidence, and return the decision to `pm-agent` or the human. Ordinary implementation details, worker handoffs, expected remediation, and file-level discoveries within approved scope are execution concerns, not reasons to re-plan.

---

## The Team

Worker behavior is defined once in [`agents/`](agents/README.md) and translated into the active tool's native prompt, skill, instruction, or agent format. Tool adapters define only native paths, delegation mechanics, model selection, and tool permissions; they must not fork the behavioral contract.

Every generated worker reads the shared contract in [`agents/README.md`](agents/README.md), its role file, the approved task, and repository instructions. Preserve the role's mission, ownership, non-responsibilities, procedure, scope boundary, evidence, blocking conditions, and handoff when specializing it for a project.

| Worker | Canonical contract | Runs when |
|---|---|---|
| `product-designer` | [`agents/product-designer.md`](agents/product-designer.md) | Product behavior or interaction intent needs an implementation-ready brief |
| `pm` | [`agents/pm.md`](agents/pm.md) | Approved intent needs an executable manifest or completion summary |
| `domain-modeler` | [`agents/domain-modeler.md`](agents/domain-modeler.md) | The approved manifest changes domain language, invariants, or state transitions |
| `api-developer` | [`agents/api-developer.md`](agents/api-developer.md) | The approved manifest changes a service/API contract |
| `test-writer` | [`agents/test-writer.md`](agents/test-writer.md) | A task needs pre-implementation or regression evidence |
| `backend-builder` | [`agents/backend-builder.md`](agents/backend-builder.md) | A task implements server-side behavior |
| `frontend-builder` | [`agents/frontend-builder.md`](agents/frontend-builder.md) | A task implements client behavior |
| `destroyer` | [`agents/destroyer.md`](agents/destroyer.md) | Changed work reaches the adversarial gate |
| `review-agent` | [`agents/review-agent.md`](agents/review-agent.md) | Changed work and destroyer evidence need an independent verdict |
| `tester` | [`agents/tester.md`](agents/tester.md) | Reviewed work reaches final risk-based verification |
| `git-committer` | [`agents/git-committer.md`](agents/git-committer.md) | Review returns `SHIP IT` and task-owned work is ready to commit |

The active adapter may add project-specific framework knowledge or narrower tool access, but must not merge roles merely for convenience. A worker can report that another role is needed; it cannot adopt that role and continue unless team-lead explicitly performs a new handoff.

### `pr-agent`

Runs the separately invoked pull-request lifecycle and cumulative review gate after or alongside coherent committed work.

- Defaults to read-only `prepare`; supports explicit `open`, `refresh`, and `review` modes.
- Reviews the complete merge-base/base-to-head diff and linked acceptance evidence; task-level PASS results are supporting evidence, not cumulative approval.
- Requires explicit human authorization before push, PR creation/editing, or posting durable provider updates.
- Produces `READY FOR HUMAN REVIEW`, `CHANGES REQUIRED`, `HUMAN DECISION REQUIRED`, or—only after required checks and blocker resolution—`READY FOR HUMAN MERGE`.
- Never implements fixes, rewrites published history, dismisses review feedback, closes issues/PRs, applies final disposition labels, or merges.
- Uses the configured state backend for durable remediation/readiness evidence; the PR remains a review surface rather than the execution ledger.
- **Tools**: Read, Glob, Grep, Bash; provider mutations only after mode-specific authorization

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

## 🧭 Planning Classifications
- Applicable standards and specialist phases: <items with rationale>
- Explicitly not applicable: <items with rationale>
- Required quality/runtime evidence: <items or n/a>
- Assumptions that would reopen planning: <items or none>

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
  [per sprint]: applicable planned specialist phases → [per task]: planned test evidence → build → build-gate → destroy → review → commit → tester/smoke → pm summary
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
- Final risk-based verification (`tester` role)
- Sprint summary (`pm` role)
- Brainstorming and planning (interactive, with the user)
- Execution planning (performed by the Team Lead from the approved plan and dependency graph)
- Refinement (interactive Q&A handoff)

---

## How Execution Works

Enter through the active AI tool's `team-lead` prompt or agent. The Team Lead:

1. Reads the approved sprint issue/file from the selected state backend and consumes its scope, exclusions, acceptance criteria, and planning classifications without reproducing the PM analysis.
2. Performs an execution-readiness preflight, then builds the dependency graph and proposes the execution order for human approval when required.
3. Executes sprints in sequence and may delegate independent tasks concurrently only when the tool supports safe isolation.
4. Runs only the specialist phases classified as applicable in the approved manifest, followed by the per-task pipeline.
5. Runs applicable `test-writer` evidence → assigned builder → build gate → `destroyer` → `review-agent` (up to 6 attempts) → `git-committer` for each task.
6. Applies the Pull Request Size Checkpoint after each committed task or coherent batch.
7. Delegates the approved risk-based sprint verification to `tester`, then has the `pm` role write the completion summary.
8. Records every durable status transition and report in the selected state backend.
9. Reopens planning only under the Planning Authority and Execution Authority conditions; otherwise resolves implementation details inside the execution loop.

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

### Execution-readiness preflight

Before starting workers, the Team Lead:

1. confirms the plan is approved and identifies the authoritative sprint record;
2. reads the recorded scope, exclusions, dependencies, acceptance criteria, planning classifications, and verification requirements;
3. checks only for execution blockers: missing required inputs, internal contradictions, stale material assumptions, repository-rule conflicts, or impossible dependency ordering;
4. records any evidence-backed planning return without attempting to redesign the sprint itself;
5. otherwise starts execution without rerunning completed planning analyses.

A preflight is not a second planning phase. Do not generate a parallel impact analysis, reinterpret explicit applicability decisions, or load a specialist solely to reconsider the PM's classification. If implementation later crosses an assumption named in the manifest, use the planning-return conditions above.

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

#### Step 1: Applicable Pre-build Specialist Phases

Run only the pre-build roles classified as applicable in the approved manifest, in dependency order. For example:

- `domain-modeler` when the sprint defines or changes domain entities, aggregates, value objects, events, or commands;
- `api-developer` when the sprint defines or changes an API contract that builders must share.

Skip roles explicitly classified as not applicable. Do not invoke a specialist to redo the classification. If a builder discovers that an omitted phase is required because the implementation would cross an approved scope or contract boundary, pause the affected task and return that evidence for planning rather than silently expanding the sprint.

#### Step 2: Confirm Shared Build Inputs

Before task implementation, confirm that every applicable domain model, API contract, design artifact, or other planned shared input is available to the assigned builders. This is an execution dependency check, not a new design phase.

#### Step 3: Per-Task Pipeline

For each task in the Sprint:

1. **`test-writer` when applicable** — writes the repository-appropriate unit and/or integration tests required by the task's contract and risk; new-behavior tests must fail at write time, while regression tests for existing guarantees may already pass and should be recorded as resilient evidence. When the approved manifest classifies test-writing as not applicable, record its planned non-test verification instead of invoking the role.
2. **Assigned builders** (`backend-builder` / `frontend-builder`) — write code until all task tests and verification pass
3. **Build gate** — the repository's documented build command must exit 0 before the destroyer runs. If it fails, the error is fed back to the builder. Code that does not compile never reaches the reviewer.
4. **`destroyer`** — adversarial testing scoped to this task's code only. Only critical/high findings are actionable. Medium/low are noted but do not block.
5. **`review-agent`** — triages destroyer findings, routes fixes to builders, escalates big issues
6. **`git-committer`** — commits after `SHIP IT`, then measures and reports the Pull Request Size Checkpoint
7. **Branch growth gate** — at the strong checkpoint, do not start another independent feature task without a human decision; finish only the smallest coherent stabilization required for a reviewable branch
8. **Failed task cleanup** — if a task exceeds max review attempts, preserve unrelated and pre-existing work, then restore only task-owned uncommitted changes using the recorded task baseline. Never use blanket checkout/clean commands on a mixed working tree. If task-owned changes cannot be separated safely, stop and ask the human rather than risk data loss.

#### Step 4: Final Tester / Sprint Smoke Test

After all tasks complete (before the PM summary), team-lead delegates final risk-based verification to [`tester`](agents/tester.md). The tester derives its scope from the approved manifest, final diff, acceptance criteria, changed boundaries, and unresolved review risks.

At minimum, run the repository's documented required build and test commands. Use focused evidence for isolated changes and expand to integration, runtime, browser, or broader suites when the approved plan, repository policy, or blast radius requires it. Reuse earlier evidence only when it still applies to the final reviewed code.

The tester posts `## 🧪 Test Report: <sprint-or-task-id> Round <N>` with `PASS`, `FAIL`, or `RISK ACCEPTANCE REQUIRED`. A required failure or unchecked blocking boundary stops the pipeline and updates the selected state backend to ✋/❌. **A sprint is not ready for acceptance verification unless tester returns `PASS` or the human explicitly accepts a reported risk under repository policy.**

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
- Builders can propose API contract changes through the API Developer, but material contract changes still return to planning
- Destroyer findings below a severity threshold get auto-routed for remediation without human involvement
- Coordinator can adjust execution order and worker routing within approved scope; scope changes still follow the planning-return contract

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
| Canonical worker contracts | `instructions/agents/*.md` | Orchestration maintainers |
| Front-door and worker definitions | Tool-specific prompts, skills, instructions, or agent files generated from canonical contracts | Setup (one-time) — see `TOOL-*.md` for format |
| Diagnostic logs/session state | Tool-specific runtime location, untracked | Active AI tool |
