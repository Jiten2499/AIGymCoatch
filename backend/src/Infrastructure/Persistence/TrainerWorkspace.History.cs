using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class TrainerWorkspace
{
    private async Task<IReadOnlyList<CoachHistoryRow>> History(Guid tenantId, Guid trainerId, IReadOnlyList<TrainerMemberAssignment> assignments, CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.AsNoTracking().FirstAsync(item => item.TenantId == tenantId && item.Id == trainerId, cancellationToken).ConfigureAwait(false);
        var subject = (await Names([trainer.UserId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(trainer.UserId, "Trainer");
        var rows = new List<CoachHistoryRow>();

        var audits = await _db.AuditLogs.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.EntityId == trainerId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var actorIds = audits.Select(item => item.UserId).Where(id => id is not null).Select(id => id!.Value)
            .Concat(assignments.Select(item => item.AssignedByUserId))
            .Distinct()
            .ToArray();
        var actors = await Names(actorIds, cancellationToken).ConfigureAwait(false);
        foreach (var item in audits)
        {
            var performedBy = item.UserId is Guid actorId && actors.TryGetValue(actorId, out var actorName) ? actorName : "System";
            rows.Add(MapAudit(item, subject, performedBy));
        }

        var memberIds = assignments.Select(item => item.MemberProfileId).Distinct().ToArray();
        var memberNames = await MemberNames(tenantId, memberIds, cancellationToken).ConfigureAwait(false);
        foreach (var item in assignments)
        {
            var member = memberNames.GetValueOrDefault(item.MemberProfileId, "Member");
            var at = item.CreatedAt == default
                ? DateTime.SpecifyKind(item.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                : item.CreatedAt;
            var ended = item.Status == TrainerAssignmentStatus.Ended;
            rows.Add(new CoachHistoryRow(
                $"assignment:{item.Id}:{item.Status}",
                ended && item.EndDate is DateOnly end ? DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : at,
                "Assignment",
                ended ? "AssignmentEnded" : "MemberAssigned",
                ended ? "Member assignment ended" : "Member assigned",
                ended
                    ? $"{member} assignment ended{(item.EndDate is null ? "" : $" on {item.EndDate:yyyy-MM-dd}")}."
                    : $"{member} assigned from {item.StartDate:yyyy-MM-dd}.",
                subject,
                actors.GetValueOrDefault(item.AssignedByUserId, "Staff"),
                ended ? "Active" : null,
                ended ? "Ended" : "Active"));
        }

        var sessions = await _db.PtSessions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sessionMemberIds = sessions.Select(item => item.MemberProfileId).Distinct().ToArray();
        var sessionMembers = await MemberNames(tenantId, sessionMemberIds, cancellationToken).ConfigureAwait(false);
        foreach (var item in sessions)
        {
            var member = sessionMembers.GetValueOrDefault(item.MemberProfileId, "Member");
            var (action, activity, at) = item.Status switch
            {
                PtSessionStatus.Completed => ("SessionCompleted", "PT session completed", item.CompletedAtUtc ?? item.CreatedAt),
                PtSessionStatus.Cancelled => ("SessionCancelled", "PT session cancelled", item.UpdatedAt == default ? item.CreatedAt : item.UpdatedAt),
                _ => ("SessionScheduled", "PT session scheduled", item.CreatedAt),
            };
            rows.Add(new CoachHistoryRow(
                $"session:{item.Id}:{item.Status}",
                at,
                "Session",
                action,
                activity,
                $"{member} · {item.ScheduledStartUtc:yyyy-MM-dd HH:mm} UTC · {item.Status}.",
                subject,
                "System",
                null,
                item.Status.ToString()));
        }

        var targets = await _db.TrainerTargets.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var item in targets)
        {
            rows.Add(new CoachHistoryRow(
                $"target:{item.Id}",
                item.CreatedAt,
                "Target",
                "TargetAssigned",
                "Monthly target assigned",
                $"{item.Name ?? item.TargetType.ToString()} · goal {item.TargetValue} · {item.PeriodStart:yyyy-MM-dd} to {item.PeriodEnd:yyyy-MM-dd} · {(item.IsActive ? "Active" : "Inactive")}.",
                subject,
                "Staff",
                null,
                item.IsActive ? "Active" : "Inactive"));
        }

        var creditAssignments = await _db.TrainerTargetAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var item in creditAssignments)
        {
            rows.Add(new CoachHistoryRow(
                $"credit-target:{item.Id}",
                item.CreatedAt,
                "Target",
                "CreditTargetAssigned",
                "Coach credit target assigned",
                $"{item.SnapshotName} · metric {item.SnapshotMetric} · goal {item.GoalValue} · {item.SnapshotAwardPoints} pts.", // enum renders as name
                subject,
                "Staff",
                null,
                item.Status.ToString()));
        }

        var credits = await _db.TrainerRewardTransactions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var item in credits)
        {
            var (action, activity) = item.TransactionType switch
            {
                TrainerRewardTransactionType.Award => ("CreditAwarded", "Coach credits awarded"),
                TrainerRewardTransactionType.Adjustment => ("CreditAdjusted", "Coach credits adjusted"),
                TrainerRewardTransactionType.Reversal => ("CreditReversed", "Coach credits reversed"),
                _ => ("CreditUpdated", "Coach credits updated"),
            };
            rows.Add(new CoachHistoryRow(
                $"credit:{item.Id}",
                item.CreatedAt,
                "Credit",
                action,
                activity,
                $"{item.Points} points · {item.ApprovalStatus}{(string.IsNullOrWhiteSpace(item.Reason) ? "" : $" · {item.Reason}")}.",
                subject,
                "Staff",
                null,
                $"{item.TransactionType}/{item.ApprovalStatus}"));
        }

        var calculations = await _db.TrainerCompensationCalculations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var item in calculations)
        {
            var (action, activity) = item.Status switch
            {
                CompensationStatus.Approved => ("CommissionApproved", $"{item.Kind} approved"),
                CompensationStatus.Rejected => ("CommissionRejected", $"{item.Kind} rejected"),
                CompensationStatus.PendingApproval => ("CommissionPending", $"{item.Kind} submitted for approval"),
                _ => ("CommissionCalculated", $"{item.Kind} calculated"),
            };
            rows.Add(new CoachHistoryRow(
                $"commission:{item.Id}:{item.Status}",
                item.CreatedAt,
                "Commission",
                action,
                activity,
                $"{item.Amount} for {item.PeriodStart:yyyy-MM-dd} to {item.PeriodEnd:yyyy-MM-dd}. {item.Basis}",
                subject,
                "System",
                null,
                item.Status.ToString()));
        }

        return rows
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.At)
            .Take(200)
            .ToArray();
    }

    private static CoachHistoryRow MapAudit(AuditLog item, string subject, string performedBy)
    {
        var action = item.Action;
        var category = action switch
        {
            var value when value.Contains("Assign", StringComparison.OrdinalIgnoreCase) => "Assignment",
            var value when value.Contains("Target", StringComparison.OrdinalIgnoreCase) => "Target",
            var value when value.Contains("Commission", StringComparison.OrdinalIgnoreCase) || value.Contains("Incentive", StringComparison.OrdinalIgnoreCase) || value.Contains("Compensation", StringComparison.OrdinalIgnoreCase) => "Commission",
            var value when value.Contains("Reward", StringComparison.OrdinalIgnoreCase) || value.Contains("Credit", StringComparison.OrdinalIgnoreCase) => "Credit",
            var value when value.Contains("Status", StringComparison.OrdinalIgnoreCase) || value.Contains("Employment", StringComparison.OrdinalIgnoreCase) => "Employment",
            var value when value.Contains("Photo", StringComparison.OrdinalIgnoreCase) || value.Contains("Create", StringComparison.OrdinalIgnoreCase) || value.Contains("Update", StringComparison.OrdinalIgnoreCase) || value.Contains("Trainer", StringComparison.OrdinalIgnoreCase) => "Profile",
            _ => "Profile",
        };
        var activity = action switch
        {
            "CreateTrainer" or "TrainerCreated" => "Trainer profile created",
            "UpdateTrainer" or "TrainerUpdated" => "Trainer profile updated",
            "TrainerPhotoUpdated" or "TrainerPhotoSet" => "Trainer photo updated",
            "TrainerPhotoRemoved" => "Trainer photo removed",
            "TrainerStatusChanged" => "Trainer status changed",
            "TrainerMemberAssigned" => "Member assigned",
            "TrainerMemberUnassigned" => "Member assignment ended",
            _ => SplitWords(action),
        };
        var details = string.IsNullOrWhiteSpace(item.NewValues)
            ? $"{item.Entity} change recorded."
            : ReadableValue(item.NewValues);
        return new CoachHistoryRow(
            $"audit:{item.Id}",
            item.CreatedAt,
            category,
            action,
            activity,
            details,
            subject,
            performedBy,
            string.IsNullOrWhiteSpace(item.OldValues) ? null : ReadableValue(item.OldValues),
            string.IsNullOrWhiteSpace(item.NewValues) ? null : ReadableValue(item.NewValues));
    }

    private static string SplitWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Activity recorded";
        }

        var chars = new List<char>(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(index == 0 ? char.ToUpperInvariant(current) : current);
        }

        return new string(chars.ToArray());
    }

    private static string ReadableValue(string value)
    {
        if (Guid.TryParse(value, out _))
        {
            return "Related record updated.";
        }

        return value.Length > 240 ? value[..237] + "..." : value;
    }
}
