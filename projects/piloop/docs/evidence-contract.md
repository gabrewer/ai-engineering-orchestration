# PiLoop Evidence Contract

PiLoop uses GitHub Issues as a durable evidence journal for work that needs to be defended later.

The goal is to capture:

- what we wanted to accomplish
- what we did to accomplish it
- decisions made and why
- blockers and mitigations
- remaining issues
- test and validation results
- artifacts produced

## Storage model

- Repo artifacts are the source of truth for generated plans and implementation files.
- GitHub Issues are the durable human-readable evidence ledger.
- `.piloop/` logs are temporary diagnostic detail and should not be committed.

## EvidenceEvent fields

PiLoop renders `EvidenceEvent` records into GitHub issue comments.

Required sections:

1. Intent
2. Plan
3. Work Performed
4. Decisions
5. Blockers / Risks
6. Test Results
7. Remaining Issues
8. Artifacts
9. Summary

## Comment discipline

GitHub Issues should receive evidence at meaningful boundaries only:

- sprint/task planned
- source delta audit completed
- implementation-ready work breakdown completed
- worker completed meaningful work
- blocker or escalation
- decision that changes direction
- accepted deviation recorded
- destroyer/reviewer/tester quality gate result
- test, browser, Aspire/runtime, or verification result
- final summary comment
- Ready for Acceptance Verification comment

Agents and PiLoop automation must never close GitHub issues and must never apply final completion/disposition labels such as `done`, `complete`, or `shipped`. GitHub issues stay open until a human verifies acceptance criteria and disposes the issue. Automation may post evidence, final summary, and acceptance-verification comments only.

## Ready for Acceptance Verification

A final summary is not enough for acceptance. For every completed task/sprint, PiLoop must prepare a `## 🧑‍⚖️ Ready for Acceptance Verification: <id>` comment.

That comment must be derived from the **original** source of truth — acceptance criteria, scope, design spec, PRD, source delta audit, planner/reference pages, or issue body — not from the implementation summary. It must include:

- a checklist mapped to the original acceptance criteria/scope;
- manual verification steps for the human;
- expected results;
- source references, screenshots, planner/reference pages, or artifacts to inspect;
- unresolved risks, accepted deviations, and remaining deltas;
- a clear statement that tests/commits are implementation evidence only and do not equal acceptance;
- a reminder that the issue remains open until a human verifies and closes/labels it.

Raw RPC events, encrypted provider payloads, token logs, stdout spam, and low-level tool traces belong in `.piloop/`, not GitHub Issues.

## Source audit evidence for parity/migration work

When the work is "make X match Y" or "port/migrate this behavior," PiLoop should produce a source-based audit before coding. The audit belongs in the selected state backend and should include:

- reference/source files read
- target/current files read
- executive summary of the largest gaps
- a delta matrix with: area, reference behavior, current behavior, status/required fix, and source references
- explicit accepted deviations or decisions still needed

The completion summary must close the loop by resolving every audit row with source evidence plus test/browser/runtime evidence. This prevents vague "parity complete" claims and makes later review defensible.
