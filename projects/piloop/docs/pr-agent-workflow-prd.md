# Standalone Pull Request Agent Workflow PRD

**Status:** Proposed for later implementation

**Product area:** PiLoop / harness-independent orchestration

**Working name:** PR Agent

**One-liner:** A separately invoked workflow that prepares, opens, refreshes, and reviews pull requests without becoming part of the team-lead implementation loop.

## Problem

PiLoop's team-lead workflow owns implementation, task-level quality gates, commits, and acceptance evidence. Pull-request lifecycle work currently happens outside that workflow without a consistent process for:

- deciding whether a branch is a coherent review unit;
- detecting branches that have accumulated too many commits or changed files;
- preparing an accurate PR title and body from issues, commits, and evidence;
- opening or updating a draft PR with explicit user authorization;
- reviewing the cumulative diff from the PR base rather than trusting task-level PASS results;
- collecting CI and human review feedback into an actionable remediation plan;
- determining when a PR is ready for a human merge decision.

Adding these responsibilities to team-lead would blur ownership and make implementation automatically imply remote GitHub operations. The PR lifecycle needs a dedicated, explicitly invoked workflow.

## Audience

Primary users are developers and technical leads using PiLoop or another supported coding harness who want agents to help manage PR readiness while retaining human control over pushing, PR creation, review disposition, and merging.

Secondary users are repository maintainers who need consistent, auditable PR practices across projects without coupling those practices to one language, framework, or hosting topology.

## Goals

1. Keep PR lifecycle management separate from team-lead.
2. Provide one explicit PR workflow with safe prepare, open, refresh, and review modes.
3. Apply the canonical branch-size checkpoints from `instructions/TEAM-ORCHESTRATION.md`.
4. Review the cumulative PR diff against its actual base.
5. Reuse team-lead evidence without treating task-level gates as cumulative PR approval.
6. Keep push, PR creation, and merge authority under explicit human control.
7. Support GitHub first while keeping the underlying role and state model hosting-provider-neutral where practical.
8. Produce concise, accessible terminal and markdown output that does not rely on color alone.

## Non-Goals

- Replacing team-lead, PM/planner, destroyer, reviewer, tester, or git-committer.
- Automatically merging pull requests.
- Automatically dismissing human review comments.
- Automatically force-pushing or rewriting published history.
- Closing issues or applying final completion/disposition labels.
- Treating a PR description as the durable execution state backend.
- Implementing arbitrary product fixes without an explicit remediation request.
- Guaranteeing that every large branch can be mechanically split after the work is complete.
- Owning deployment or release orchestration.

## Role Boundary

### Team-lead owns

- reading and implementing the sprint or issue;
- routing builder work;
- task-level build, destroyer, reviewer, and tester loops;
- creating coherent commits through git-committer;
- recording implementation and acceptance evidence in the selected state backend;
- preparing the feature for user acceptance verification.

Team-lead does not invoke PR Agent automatically and does not create, push, update, or merge a PR unless the user separately invokes and authorizes the PR workflow.

### PR Agent owns

- detecting branch, base, and existing PR context;
- branch-size and coherence assessment;
- cumulative diff readiness analysis;
- proposed PR title/body generation;
- authorized branch push and PR creation;
- existing PR body/evidence refresh;
- CI and review feedback collection;
- remediation-plan generation;
- human merge-readiness reporting.

### Human owns

- authorizing remote push and PR creation;
- deciding whether to continue past a strong size checkpoint;
- accepting intentional gaps or risks;
- resolving product/architecture decisions that exceed agent authority;
- merging or closing the pull request.

## Invocation Model

The project should expose a harness-appropriate front door with four modes:

```text
/pr-agent prepare [issue-or-branch]
/pr-agent open [issue-or-branch] [draft|ready]
/pr-agent refresh [pr-number-or-url]
/pr-agent review [pr-number-or-url]
```

Equivalent CLI commands may be added later, for example:

```bash
piloop pr prepare
piloop pr open --draft
piloop pr refresh 123
piloop pr review 123
```

Mode selection must be explicit. `prepare` is the safe default when no mode is supplied.

## Functional Requirements

### PR-001: Prepare mode is read-only

Prepare mode must not push, create a PR, edit a PR, alter review conversations, rebase, commit, or modify source files.

It must:

1. Detect the current branch and reject direct PR preparation from `main` or `master` unless it is only reporting that a feature branch is required.
2. Detect an existing PR and its base/head/state when provider metadata is available.
3. Otherwise identify the repository's configured mainline or require an explicit base.
4. Measure commits and unique changed files from the base to `HEAD`.
5. Apply the canonical `BELOW`, `ADVISORY`, or `STRONG` size checkpoint.
6. Inspect working-tree and staged state.
7. Identify commits or files that appear unrelated to the proposed PR purpose.
8. Determine whether the branch is one coherent review unit or should be split.
9. Read linked issues and available team-lead evidence from the selected state backend.
10. Generate, but not publish, a proposed PR title and body.
11. Report blockers, missing evidence, unresolved risks, and the next recommended action.

### PR-002: Open mode requires explicit authorization

Open mode is the explicit authorization boundary for remote side effects.

Before pushing or creating a PR, it must:

- show branch, base, commit count, changed-file count, checkpoint status, and proposed PR state;
- refuse `main`/`master` as the head branch;
- refuse known secrets, temporary artifacts, or unrelated staged changes;
- refuse when required task-owned changes remain uncommitted;
- refuse a ready-for-review PR when known BLOCKERs or required checks are failing;
- allow a draft PR with documented incomplete checks when the branch is intentionally being exposed early for review;
- use normal push only for an unpublished branch;
- never use plain `--force`; any authorized history update must follow repository policy and use `--force-with-lease` only when explicitly required.

After authorization, it may:

1. Push the feature branch.
2. Create a draft or ready PR.
3. Link issues without closing them unless repository policy and the user explicitly request closing semantics.
4. Publish the generated PR body.
5. Record the PR URL in the selected state backend.

Open mode must never merge the PR.

### PR-003: Refresh mode keeps PR metadata truthful

Refresh mode must compare the current PR body and evidence with the latest branch state.

It must report and optionally update, with authorization:

- scope represented by the cumulative diff;
- linked issues and acceptance criteria;
- Contract Impact Check or equivalent risk summary;
- security boundaries used or exposed by the change;
- migrations and deployment/configuration changes;
- build, test, accessibility, runtime, and review evidence;
- unresolved blockers, warnings, accepted gaps, and deployment blockers;
- commit and changed-file counts plus checkpoint state;
- newly added commits/files not represented in the PR description.

At a strong checkpoint, refresh mode must recommend stopping scope growth. If additional independent work exists, it should recommend a follow-up or stacked PR rather than silently expanding the current PR.

### PR-004: Review mode performs a cumulative PR gate

Review mode must assess the full diff from the PR base to `HEAD`. Task-level reviews are supporting evidence, not substitutes for this gate.

The cumulative gate must include, where applicable:

- complete changed endpoint and public contract inventory;
- authentication, authorization, ownership, and tenancy boundaries invoked, extended, exposed, or made more consequential;
- cross-task integration and state precedence;
- persistence, migration, and concurrency invariants;
- route, gateway, deployment, and service-topology alignment;
- cumulative frontend and accessibility behavior;
- original acceptance criteria across all linked issues;
- test coverage for rejection, failure, no-mutation, and concurrency paths;
- runtime/browser evidence and explicitly unverified paths;
- release or deployment blockers.

Review mode must produce one of:

- `READY FOR HUMAN REVIEW`
- `CHANGES REQUIRED`
- `HUMAN DECISION REQUIRED`

It must not emit `READY FOR HUMAN MERGE` until required CI/checks pass and unresolved blocker conversations are cleared or explicitly accepted by a human.

### PR-005: Review feedback becomes tracked remediation input

Review mode must read provider check results, review summaries, inline comments, and unresolved conversations when available.

For each actionable finding, it must record:

- provider review/comment identifier;
- severity;
- file and line/context;
- requested or inferred corrective outcome;
- owning role or suggested skill;
- required verification;
- whether the finding affects the cumulative PR gate.

The remediation plan must be posted to or linked from the selected durable state backend. PR Agent must not make product fixes unless the user explicitly requests a remediation execution workflow.

After remediation, review mode must verify changed findings and identify regressions in the same affected area. Material remediation must trigger a fresh cumulative assessment.

### PR-006: Human merge readiness is explicit

When all required conditions are met, PR Agent may report `READY FOR HUMAN MERGE` with:

- PR URL, base, and head;
- commit and changed-file counts;
- cumulative gate verdict;
- required check results;
- unresolved warnings and accepted risks;
- linked issue/acceptance status;
- deployment/release blockers;
- exact manual verification still expected from the human.

The report must state that the agent has not merged the PR.

## Pull Request Body Contract

Generated PR bodies should contain:

1. **Summary** — concise user/system outcome.
2. **Scope** — included and explicitly excluded work.
3. **Linked issues/specs** — source-of-truth references.
4. **Contract and security impact** — API, persistence, auth/ownership/tenancy, migrations, remote dependencies, deployment.
5. **What changed** — grouped by coherent concern rather than commit chronology.
6. **Verification** — exact commands and results.
7. **Accessibility** — keyboard, screen-reader/accessibility-tree, labels/errors, dynamic updates, zoom/reduced motion, or not applicable.
8. **Known gaps and blockers** — including checks not run.
9. **Deployment/release notes** — feature flags, migrations, configuration, rollout, or explicitly none.
10. **Human review checklist** — review targets and manual verification.

PR bodies must not contain secrets, sensitive customer data, raw tokens/session identifiers, or temporary local paths.

## Branch Size and Splitting Behavior

PR Agent consumes the canonical defaults from `instructions/TEAM-ORCHESTRATION.md`:

- Advisory: 8 commits or 30 changed files.
- Strong: 15 commits or 60 changed files.

Repositories may override these thresholds explicitly.

When recommending a split, PR Agent must propose concrete slices using file ownership, commit dependencies, public contracts, and runtime dependencies. It must identify whether slices can be independent PRs or must be stacked.

PR Agent must not rewrite history or perform the split automatically in the initial implementation. Automated split assistance may be considered later after repository-specific safety requirements are defined.

## State and Evidence

The existing user-selected state backend remains authoritative:

- **GitHub Issues mode:** PR Agent posts or links readiness/remediation updates in the relevant issue and stores temporary body drafts under `.agentloop/tmp/` or the harness-specific ignored temp path.
- **Filesystem mode:** PR Agent writes durable readiness/remediation artifacts only to the repository-defined state paths.

The PR itself is a review surface, not the sole execution ledger.

A minimal provider-neutral PR state should include:

```json
{
  "mode": "prepare|open|refresh|review",
  "provider": "github",
  "base": "main",
  "head": "feature/example",
  "pullRequestUrl": null,
  "commitCount": 0,
  "changedFileCount": 0,
  "checkpoint": "below|advisory|strong",
  "cumulativeVerdict": "not-run|ready|changes-required|human-decision-required",
  "requiredChecks": [],
  "unresolvedFindings": []
}
```

Local runtime state is temporary and must not be committed unless it is a deliberate durable project artifact.

## Safety and Permission Model

| Action | Prepare | Open | Refresh | Review |
|---|---:|---:|---:|---:|
| Read git/provider metadata | Yes | Yes | Yes | Yes |
| Read issues/evidence | Yes | Yes | Yes | Yes |
| Generate title/body locally | Yes | Yes | Yes | Yes |
| Modify source code | No | No | No | No |
| Commit changes | No | No | No | No |
| Push branch | No | Explicitly authorized | No | No |
| Create PR | No | Explicitly authorized | No | No |
| Edit PR body | No | As part of authorized creation | Explicitly authorized | No |
| Post issue/remediation update | No by default | Authorized as part of open | Explicitly authorized | Explicitly authorized |
| Dismiss review comments | No | No | No | No |
| Merge PR | No | No | No | No |

Provider operations must use least-privilege credentials and respect repository branch protections.

## Accessibility and Output Requirements

- Terminal output must include text labels for status and severity; color may supplement but never replace words.
- Reports must use headings, lists, and tables that remain understandable in plain text and screen readers.
- Interactive confirmation must present the exact remote side effects before authorization.
- Prompts must not rely on pointer input.
- Long reports should provide a concise summary first and stable identifiers for findings.

## Observability and Auditability

Record, without secrets:

- mode and invocation time;
- branch/base and PR URL;
- commit/file counts and checkpoint;
- provider operations attempted and result;
- cumulative gate verdict;
- check/review identifiers consumed;
- durable state update location;
- explicit user authorization or accepted gap when required.

## Technical Considerations

### Harness-independent core

The core workflow should define provider-neutral commands and result models for:

- repository/branch inspection;
- diff and checkpoint measurement;
- evidence collection;
- PR body generation;
- cumulative gate reporting.

### GitHub adapter

The initial provider adapter may use `gh` for:

- `gh pr view`
- `gh pr create`
- `gh pr edit`
- `gh pr checks`
- `gh pr view --comments`
- GitHub API queries for review threads where CLI coverage is insufficient.

All mutating provider calls require the mode-specific authorization described above.

### Pi adapter

Pi should expose a thin `.pi/prompts/pr-agent.md` front door and a reusable `.agents/skills/pr-agent/SKILL.md` or equivalent role definition. The prompt must load the canonical PR workflow rather than duplicating policy.

Other tool adapters should map the same modes and permissions into their native command/agent formats.

## Acceptance Criteria

- [ ] PR Agent is separately invoked and never runs automatically as part of team-lead.
- [ ] Prepare mode performs no local or remote mutation.
- [ ] Open mode cannot push or create a PR without explicit user authorization.
- [ ] No mode can merge a PR.
- [ ] Existing PR base/head/state are detected correctly when available.
- [ ] Commit/file counts and checkpoint status match git/provider truth.
- [ ] Strong checkpoint prevents the workflow from recommending additional unreviewed scope without a human decision.
- [ ] Proposed PR title/body accurately represent the cumulative diff and linked issue evidence.
- [ ] Cumulative review examines the full base-to-head diff rather than only the latest task.
- [ ] Inherited security boundaries are included in cumulative review scope.
- [ ] Review feedback is converted into stable, traceable remediation findings.
- [ ] Missing required security/runtime evidence is reported as `NOT CHECKED` and blocks readiness unless explicitly accepted.
- [ ] Ready-for-human-merge output includes checks, warnings, accepted risks, deployment blockers, and manual verification.
- [ ] Temporary provider body/state files are ignored and not committed.
- [ ] Output remains understandable without color and through a screen reader.

## Milestones

### Milestone 1: Prepare-only workflow

- Define provider-neutral request/result models.
- Detect branch/base/existing PR.
- Measure checkpoints and branch coherence.
- Generate a local title/body/readiness report.
- Add tests proving no mutation.

### Milestone 2: Authorized open and refresh

- Add explicit side-effect confirmation.
- Push unpublished feature branches safely.
- Create draft/ready GitHub PRs.
- Refresh body and evidence with authorization.
- Record PR URL in the selected state backend.

### Milestone 3: Cumulative review and feedback ingestion

- Review the complete PR diff.
- Collect CI/check and review feedback.
- Produce traceable remediation plans.
- Re-evaluate material remediation.

### Milestone 4: Multi-harness adapters

- Add Pi prompt/skill.
- Add Claude Code, GitHub Copilot, and opencode adapters.
- Verify consistent permissions and outputs across harnesses.

## Open Questions

1. Should `open` default to draft even when all checks pass, requiring an explicit `ready` argument?
2. Should refresh authorization be granted once per invocation or once per PR session?
3. Which GitHub review-thread API should be the canonical source for unresolved conversations?
4. Should the first implementation only recommend branch splits, or also generate a safe cherry-pick/stack plan?
5. How should provider-neutral interfaces model stacked PR dependencies?
6. Which required CI checks should be inferred from branch protection versus repository instructions?
7. Should `READY FOR HUMAN MERGE` require user acceptance verification, or may repositories configure PR review and UAT as separate gates?
8. Should PR Agent create a durable issue comment in prepare mode, or remain fully read-only until explicitly authorized?
