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
- worker completed meaningful work
- blocker or escalation
- decision that changes direction
- test or verification result
- final summary

Raw RPC events, encrypted provider payloads, token logs, stdout spam, and low-level tool traces belong in `.piloop/`, not GitHub Issues.
