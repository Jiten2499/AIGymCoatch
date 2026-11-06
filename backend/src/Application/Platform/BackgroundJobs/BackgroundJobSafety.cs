using System.Text.RegularExpressions;

using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Platform.BackgroundJobs;

public static class BackgroundJobErrors
{
    public static readonly Error NotFound = new("background_job.not_found", "The background job was not found.");
    public static readonly Error InvalidState = new("background_job.invalid_state", "The background job cannot change to that state.");
}

public static partial class BackgroundJobSafety
{
    public static string? Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redacted = SecretAssignment().Replace(value, "$1=(redacted)");
        redacted = Bearer().Replace(redacted, "bearer (redacted)");
        return redacted.Length <= 500 ? redacted : redacted[..500];
    }

    public static BackgroundJobStatus Normalize(string? hangfireState) =>
        hangfireState switch
        {
            "Enqueued" => BackgroundJobStatus.Queued,
            "Scheduled" => BackgroundJobStatus.Scheduled,
            "Processing" => BackgroundJobStatus.Processing,
            "Succeeded" => BackgroundJobStatus.Succeeded,
            "Failed" => BackgroundJobStatus.Failed,
            "Deleted" => BackgroundJobStatus.Deleted,
            "Awaiting" => BackgroundJobStatus.Awaiting,
            _ => BackgroundJobStatus.Queued,
        };

    public static string? ToHangfireState(BackgroundJobStatus? status) =>
        status switch
        {
            BackgroundJobStatus.Queued => "Enqueued",
            BackgroundJobStatus.Scheduled => "Scheduled",
            BackgroundJobStatus.Processing => "Processing",
            BackgroundJobStatus.Succeeded => "Succeeded",
            BackgroundJobStatus.Failed => "Failed",
            BackgroundJobStatus.Deleted => "Deleted",
            BackgroundJobStatus.Awaiting => "Awaiting",
            _ => null,
        };

    public static bool CanRetry(BackgroundJobStatus status) => status == BackgroundJobStatus.Failed;

    public static bool CanCancel(BackgroundJobStatus status) =>
        status is BackgroundJobStatus.Queued or BackgroundJobStatus.Scheduled or BackgroundJobStatus.Processing or BackgroundJobStatus.Awaiting;

    [GeneratedRegex("(?i)(password|secret|token|api[_-]?key|authorization|connectionstring)\\s*[=:]\\s*\\S+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignment();

    [GeneratedRegex("(?i)bearer\\s+\\S+", RegexOptions.CultureInvariant)]
    private static partial Regex Bearer();
}
