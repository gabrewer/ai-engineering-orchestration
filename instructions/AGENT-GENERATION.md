# Agent Generation

This file defines how to generate a project-specific AI team. It is separate from [`TEAM-ORCHESTRATION.md`](TEAM-ORCHESTRATION.md), which defines how the generated team plans and executes work.

## Purpose

Generate agents for the current project from the available source material—not a generic team copied across repositories. The source material may include a PRD, design or architecture documentation, other planning documents, and the current repository.

The generated agents, front doors, worker skills, prompts, model assignments, and tool permissions must be tailored to the described and verified environments while preserving the canonical role contracts in [`agents/README.md`](agents/README.md).

## Source authority

Use inputs in this order for the relevant concern:

1. Explicit human requirements and decisions
2. Approved PRD, sprint brief, or planning documentation
3. Repository instructions and project conventions
4. Source code, tests, configuration, and deployment files
5. Canonical worker contracts and orchestration rules

Treat documented target environments as intended environments and verify them against the repository. If documentation is absent, infer context from the repository and record assumptions. If inputs conflict or a material environment is ambiguous, ask the human before generating agents.

## Discovery and tailoring

Before generating resources, identify and encode:

- languages, frameworks, package managers, repository layout, and conventions;
- build, test, lint, format, and runtime commands;
- hosting, deployment, operating-system, browser, cloud, database, and integration environments;
- project domain terminology, architecture boundaries, security/privacy risks, and operational constraints;
- applicable specialist roles and worker routing;
- model assignments, tool permissions, quality gates, and verification commands.

Generated agents must reference project-specific files and commands, must not assume technologies or environments that are absent or unapproved, and must retain each role's mission, ownership, non-responsibilities, scope boundary, evidence, blocking conditions, and handoff.

## Generation process

1. Inspect the source inputs and repository.
2. Summarize the discovered project and environment, including assumptions and conflicts.
3. Propose a compact map of front-door agents, workers, environment tailoring, model assignments, and permissions.
4. Confirm any material ambiguity with the human.
5. Generate the active tool's native definitions using the relevant `TOOL-*.md` instructions.
6. Generate repository-level routing instructions for `pm-agent` and `team-lead`.
7. Validate that generated resources use the discovered commands, paths, and environments.

The generated resources are project configuration. This file and the canonical worker contracts remain the source of truth for generation behavior; `TEAM-ORCHESTRATION.md` remains the source of truth for planning and execution behavior.

## Output

Generation should produce the tool-specific prompts, skills, instructions, or agent files required by the active tool, plus any repository-level instruction needed to route planning through `pm-agent` and execution through `team-lead`.
