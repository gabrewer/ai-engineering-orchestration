using System.Text;
using PiLoop.Models;

namespace PiLoop.Services;

public static class EvidenceRenderer
{
    public static string RenderMarkdown(EvidenceEvent evidence)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"## Evidence: {evidence.Step}");
        builder.AppendLine($"- **Agent:** {evidence.Agent}");
        builder.AppendLine($"- **Status:** {FormatStatus(evidence.Status)}");
        if (!string.IsNullOrWhiteSpace(evidence.TaskId))
            builder.AppendLine($"- **Task:** {evidence.TaskId}");
        builder.AppendLine();

        AppendSection(builder, "Intent", evidence.Intent);
        AppendSection(builder, "Plan", evidence.Plan);
        AppendListSection(builder, "Work Performed", evidence.WorkPerformed);
        AppendDecisions(builder, evidence.Decisions);
        AppendBlockers(builder, evidence.Blockers);
        AppendTests(builder, evidence.TestResults);
        AppendRemainingIssues(builder, evidence.RemainingIssues);
        AppendListSection(builder, "Artifacts", evidence.Artifacts);
        AppendSection(builder, "Summary", evidence.Summary);

        return builder.ToString().TrimEnd();
    }

    public static EvidenceEvent FromWorkerResult(
        PiWorkerResult result,
        EvidenceTarget target,
        EvidenceStatus status,
        string agent,
        string step,
        string intent,
        string plan,
        string? taskId = null)
    {
        return new EvidenceEvent(
            target,
            status,
            agent,
            step,
            intent,
            plan,
            WorkPerformed: [result.WhatHappened],
            Decisions:
            [
                new EvidenceDecision(
                    result.Summary,
                    result.Why,
                    result.AlternativesConsidered,
                    "Captured from worker final result; no explicit tradeoff field was provided.")
            ],
            Blockers: result.Status is PiWorkerStatus.Blocked or PiWorkerStatus.Escalate or PiWorkerStatus.Failed
                ? [new EvidenceBlocker(result.Summary, result.Status.ToString(), result.NextAction)]
                : [],
            TestResults: [],
            RemainingIssues: [],
            Artifacts: result.Artifacts.Select(a => a.Path).ToArray(),
            Summary: result.Summary,
            taskId);
    }

    private static void AppendSection(StringBuilder builder, string heading, string content)
    {
        builder.AppendLine($"### {heading}");
        builder.AppendLine(string.IsNullOrWhiteSpace(content) ? "_None recorded._" : content.Trim());
        builder.AppendLine();
    }

    private static void AppendListSection(StringBuilder builder, string heading, string[] items)
    {
        builder.AppendLine($"### {heading}");
        if (items.Length == 0)
        {
            builder.AppendLine("_None recorded._");
        }
        else
        {
            foreach (var item in items)
                builder.AppendLine($"- {item}");
        }
        builder.AppendLine();
    }

    private static void AppendDecisions(StringBuilder builder, EvidenceDecision[] decisions)
    {
        builder.AppendLine("### Decisions");
        if (decisions.Length == 0)
        {
            builder.AppendLine("_None recorded._");
        }
        else
        {
            foreach (var decision in decisions)
            {
                builder.AppendLine($"- **Decision:** {decision.Decision}");
                builder.AppendLine($"  - **Why:** {decision.Why}");
                builder.AppendLine($"  - **Alternatives considered:** {(decision.AlternativesConsidered.Length == 0 ? "none" : string.Join("; ", decision.AlternativesConsidered))}");
                builder.AppendLine($"  - **Tradeoff:** {decision.Tradeoff}");
            }
        }
        builder.AppendLine();
    }

    private static void AppendBlockers(StringBuilder builder, EvidenceBlocker[] blockers)
    {
        builder.AppendLine("### Blockers / Risks");
        if (blockers.Length == 0)
        {
            builder.AppendLine("_None recorded._");
        }
        else
        {
            foreach (var blocker in blockers)
            {
                builder.AppendLine($"- **Blocker:** {blocker.Blocker}");
                builder.AppendLine($"  - **Status:** {blocker.Status}");
                builder.AppendLine($"  - **Mitigation:** {blocker.Mitigation}");
            }
        }
        builder.AppendLine();
    }

    private static void AppendTests(StringBuilder builder, EvidenceTestResult[] tests)
    {
        builder.AppendLine("### Test Results");
        if (tests.Length == 0)
        {
            builder.AppendLine("_No tests recorded for this evidence event._");
        }
        else
        {
            foreach (var test in tests)
            {
                builder.AppendLine($"- **Command:** `{test.Command}`");
                builder.AppendLine($"  - **Result:** {test.Result}");
                builder.AppendLine($"  - **Evidence:** {test.Evidence}");
            }
        }
        builder.AppendLine();
    }

    private static void AppendRemainingIssues(StringBuilder builder, EvidenceRemainingIssue[] issues)
    {
        builder.AppendLine("### Remaining Issues");
        if (issues.Length == 0)
        {
            builder.AppendLine("_None recorded._");
        }
        else
        {
            foreach (var issue in issues)
            {
                builder.AppendLine($"- **Issue:** {issue.Issue}");
                builder.AppendLine($"  - **Reason deferred:** {issue.ReasonDeferred}");
                builder.AppendLine($"  - **Next step:** {issue.NextStep}");
            }
        }
        builder.AppendLine();
    }

    private static string FormatStatus(EvidenceStatus status) => status switch
    {
        EvidenceStatus.InProgress => "in progress",
        _ => status.ToString().ToLowerInvariant(),
    };
}
