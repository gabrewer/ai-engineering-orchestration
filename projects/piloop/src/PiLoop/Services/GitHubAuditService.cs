using System.Text;
using PiLoop.Models;

namespace PiLoop.Services;

public sealed class GitHubAuditService
{
    private readonly GitHubService _github;

    public GitHubAuditService(string? workDir = null)
    {
        _github = new GitHubService(workDir);
    }

    public Task UpdateTaskStatusAsync(int issueNumber, string emoji) =>
        _github.UpdateIssueStatusAsync(issueNumber, emoji);

    public Task CheckTaskOnEpicAsync(int epicNumber, string taskId) =>
        _github.CheckTaskOnEpicAsync(epicNumber, taskId);

    public Task PublishToEpicAsync(int epicIssueNumber, BreadcrumbEvent breadcrumb) =>
        PublishAsync(epicIssueNumber, breadcrumb with { Target = BreadcrumbTarget.Epic });

    public Task PublishToTaskAsync(int taskIssueNumber, BreadcrumbEvent breadcrumb) =>
        PublishAsync(taskIssueNumber, breadcrumb with { Target = BreadcrumbTarget.Task });

    public Task PublishAsync(int issueNumber, BreadcrumbEvent breadcrumb) =>
        _github.CommentOnIssueAsync(issueNumber, FormatBreadcrumbComment(breadcrumb));

    public Task PublishEvidenceToEpicAsync(int epicIssueNumber, EvidenceEvent evidence) =>
        PublishEvidenceAsync(epicIssueNumber, evidence with { Target = EvidenceTarget.Epic });

    public Task PublishEvidenceToTaskAsync(int taskIssueNumber, EvidenceEvent evidence) =>
        PublishEvidenceAsync(taskIssueNumber, evidence with { Target = EvidenceTarget.Task });

    public Task PublishEvidenceAsync(int issueNumber, EvidenceEvent evidence) =>
        _github.CommentOnIssueAsync(issueNumber, EvidenceRenderer.RenderMarkdown(evidence));

    public Task PublishCommentAsync(int issueNumber, string markdown) =>
        _github.CommentOnIssueAsync(issueNumber, markdown);

    internal static string FormatBreadcrumbComment(BreadcrumbEvent breadcrumb)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"## {breadcrumb.Step}");
        builder.AppendLine($"- **Agent:** {breadcrumb.Agent}");
        builder.AppendLine($"- **Status:** {FormatStatus(breadcrumb.Status)}");
        builder.AppendLine($"- **What happened:** {breadcrumb.WhatHappened}");
        builder.AppendLine($"- **Why:** {breadcrumb.Why}");
        builder.AppendLine($"- **Alternatives considered:** {FormatAlternatives(breadcrumb.AlternativesConsidered)}");
        builder.AppendLine($"- **Confidence:** {FormatConfidence(breadcrumb.Confidence)}");
        builder.AppendLine($"- **Next action:** {breadcrumb.NextAction}");

        if (breadcrumb.Artifacts is { Length: > 0 })
            builder.AppendLine($"- **Artifacts:** {string.Join(", ", breadcrumb.Artifacts)}");

        if (!string.IsNullOrWhiteSpace(breadcrumb.Verification))
            builder.AppendLine($"- **Verification:** {breadcrumb.Verification}");

        return builder.ToString().TrimEnd();
    }

    private static string FormatAlternatives(string[] alternatives) =>
        alternatives.Length == 0 ? "none" : string.Join("; ", alternatives);

    private static string FormatStatus(BreadcrumbStatus status) => status switch
    {
        BreadcrumbStatus.Started => "started",
        BreadcrumbStatus.Completed => "completed",
        BreadcrumbStatus.Failed => "failed",
        BreadcrumbStatus.Blocked => "blocked",
        BreadcrumbStatus.ChangesNeeded => "changes needed",
        BreadcrumbStatus.Escalated => "escalated",
        BreadcrumbStatus.Verified => "verified",
        BreadcrumbStatus.Decision => "decision",
        _ => "decision",
    };

    private static string FormatConfidence(PiWorkerConfidence confidence) => confidence switch
    {
        PiWorkerConfidence.High => "high",
        PiWorkerConfidence.Medium => "medium",
        PiWorkerConfidence.Low => "low",
        _ => "medium",
    };
}
