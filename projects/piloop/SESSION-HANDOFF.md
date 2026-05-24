# PiLoop Session Handoff

## Current Goal
Build PiLoop as a Pi-native orchestration project inside `projects/piloop/`.

PiLoop must support two workflows:
1. dropping PiLoop into a brand-new project
2. dropping PiLoop into an existing project

## Key Decisions
- PiLoop is intentionally Pi-specific, not harness-agnostic.
- Runtime details are harness-specific enough that a universal orchestrator is not the target.
- The orchestration repo `ai-engineering-orchestration` is the long-term home.
- TrakPomo is now the first consumer/integration target, not the home of the runtime.
- Planning mode should be extracted first before execution/build mode.
- GitHub Issues remain the durable audit trail.
- Workers are Pi subprocesses; the orchestrator is the boss.

## Files Created
- `projects/piloop/README.md`
- `projects/piloop/docs/architecture.md`
- `projects/piloop/docs/workflow-new-project.md`
- `projects/piloop/docs/workflow-existing-project.md`
- `projects/piloop/docs/migration-plan.md`
- `projects/piloop/src/README.md`
- `projects/piloop/examples/README.md`

## Migration Source
Primary extraction source:
- `X:/source/TrakPomo/wt/wt-codex/tools/agentloop/`

Pi-native files worth porting first:
- `Program.cs`
- `Models/BreadcrumbEvent.cs`
- `Models/PiRpcEvent.cs`
- `Models/PiRuntimeOptions.cs`
- `Models/PiWorkerResult.cs`
- `Models/Prd.cs`
- `Services/GitHubAuditService.cs`
- `Services/GitHubService.cs`
- `Services/GitService.cs`
- `Services/PiProcessHost.cs`
- `Services/PiResultValidator.cs`
- `Services/PiRpcRunner.cs`
- `Services/PiWorkerContractBuilder.cs`
- `Services/PiWorkerRegistry.cs`
- `Services/PlanningLoop.cs`
- `Services/TempLogService.cs`
- selective parts of `State.cs` and `ActivityLog.cs`

Do not port old Codex-era runtime code unless explicitly needed.

## Important Recent Work in TrakPomo
- Fixed Windows Pi command resolution by defaulting to `pi.cmd`.
- Fixed Pi RPC success handling so completed workers are not marked failed after intentional process termination.
- Fixed sprint discovery/grouping by logical sprint name rather than dated filename prefix.
- Added GitHub issue reuse/idempotency for planning runs.
- Added Pi transport failure classification:
  - Transport
  - Auth
  - RateLimit
  - Quota
  - Timeout
  - Process
  - Rpc
  - Unknown
- Added retry-on-transport-failure in `PiRpcRunner`.
- Improved worker diagnostics in logs and validator errors.

## Known Issue
A live planning rerun was interrupted by a transient OpenAI websocket transport failure through Pi.
This appeared to be transport-related, not quota-related.

## Next Steps
1. Start working in `X:/source/ai-engineering-orchestration`.
2. Scaffold `projects/piloop/src/PiLoop/PiLoop.csproj`.
3. Create the initial PiLoop CLI.
4. Introduce a target-project-root argument model.
5. Extract planning-mode Pi-native runtime first.
6. Make prompts/skills/artifact paths target-repo-relative instead of runtime-repo-relative.
7. Use TrakPomo as the first consumer example after extraction.
