using System.Globalization;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class MyDashboardReader
{
    private async Task<DashboardMetric> SessionsAsync(DashboardActorContext context, DashboardPeriod period, CancellationToken cancellationToken)
    {
        if (context.TrainerProfileId is not Guid trainerId)
        {
            var onLeave = await _db.StaffLeaveRequests.AsNoTracking().AnyAsync(
                request => request.TenantId == context.TenantId && request.UserId == context.UserId && request.Status == LeaveRequestStatus.Approved && request.StartDate <= period.Today && request.EndDate >= period.Today,
                cancellationToken).ConfigureAwait(false);
            return new DashboardMetric(onLeave ? "Leave" : "0", onLeave ? "You are on approved leave today" : "No sessions are on your schedule");
        }

        var count = await _db.PtSessions.AsNoTracking().CountAsync(
            session => session.TenantId == context.TenantId && session.TrainerProfileId == trainerId && session.Status != PtSessionStatus.Cancelled && session.ScheduledStartUtc >= period.TodayStartUtc && session.ScheduledStartUtc < period.TomorrowStartUtc,
            cancellationToken).ConfigureAwait(false);
        return Count(count, "Sessions scheduled for you today");
    }

    private async Task<DashboardMetric> AssignedMembersAsync(DashboardActorContext context, CancellationToken cancellationToken)
    {
        if (context.TrainerProfileId is not Guid trainerId)
        {
            return new DashboardMetric("0", "No coach profile is linked to this account.");
        }

        var count = await ActiveAssignments(context.TenantId, trainerId).CountAsync(cancellationToken).ConfigureAwait(false);
        return Count(count, "Members assigned to you");
    }

    private async Task<DashboardMetric> AssessmentsAsync(DashboardActorContext context, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var members = await AssignedMemberIdsAsync(context, cancellationToken).ConfigureAwait(false);
        if (members.Count == 0)
        {
            return new DashboardMetric("0", "No assigned members are waiting for an assessment.");
        }

        var recent = await _db.MemberProgressAssessments.AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && members.Contains(item.MemberProfileId) && item.AssessmentDate >= period.Today.AddDays(-30))
            .Select(item => item.MemberProfileId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        return Count(members.Count - recent, "Assigned members without an assessment in 30 days");
    }

    private async Task<DashboardMetric> ProgressAsync(DashboardActorContext context, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var members = await AssignedMemberIdsAsync(context, cancellationToken).ConfigureAwait(false);
        var count = members.Count == 0
            ? 0
            : await _db.MemberProgressAssessments.AsNoTracking().CountAsync(
                item => item.TenantId == context.TenantId && members.Contains(item.MemberProfileId) && item.AssessmentDate >= period.Today.AddDays(-7),
                cancellationToken).ConfigureAwait(false);
        return Count(count, "Assessments recorded for your members in the last 7 days");
    }

    private async Task<DashboardMetric> TargetsAsync(DashboardActorContext context, DashboardPeriod period, CancellationToken cancellationToken)
    {
        if (context.TrainerProfileId is not Guid trainerId)
        {
            return new DashboardMetric("0", "No coach profile is linked to this account.");
        }

        var goal = await _db.TrainerTargetAssignments.AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.TrainerProfileId == trainerId && item.Status == TrainerTargetAssignmentStatus.Active && item.EffectiveStart <= period.Today && (item.EffectiveEnd == null || item.EffectiveEnd >= period.Today))
            .SumAsync(item => (decimal?)item.GoalValue, cancellationToken)
            .ConfigureAwait(false) ?? 0m;
        var done = await _db.PtSessions.AsNoTracking().CountAsync(
            session => session.TenantId == context.TenantId && session.TrainerProfileId == trainerId && session.Status == PtSessionStatus.Completed && session.CompletedOn >= period.MonthStart && session.CompletedOn <= period.Today,
            cancellationToken).ConfigureAwait(false);
        return new DashboardMetric($"{done} / {goal.ToString("0", CultureInfo.InvariantCulture)}", "Completed PT sessions against active targets");
    }

    private async Task<DashboardMetric> CreditsAsync(DashboardActorContext context, CancellationToken cancellationToken)
    {
        if (context.TrainerProfileId is not Guid trainerId)
        {
            return new DashboardMetric("0", "No coach profile is linked to this account.");
        }

        var points = await _db.TrainerRewardTransactions.AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.TrainerProfileId == trainerId && item.ApprovalStatus == TrainerRewardApprovalStatus.Approved)
            .SumAsync(item => (int?)item.Points, cancellationToken)
            .ConfigureAwait(false) ?? 0;
        return Count(points, "Approved coach credits");
    }

    private async Task<DashboardMetric> OutstandingAsync(Guid tenantId, Guid? branchId, CancellationToken cancellationToken)
    {
        var rows = await Memberships(tenantId, branchId)
            .Where(membership => membership.Status == GymMembershipStatus.Active)
            .Select(membership => new
            {
                membership.FinalAmount,
                Paid = _db.MembershipPayments.Where(payment => payment.TenantId == tenantId && payment.GymMembershipId == membership.Id && payment.Status == MembershipPaymentStatus.Posted).Sum(payment => (decimal?)payment.Amount) ?? 0m,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var total = rows.Sum(row => MembershipPaymentRules.Outstanding(row.FinalAmount, row.Paid));
        var currency = rows.Count == 0 ? "USD" : await _db.BusinessProfiles.AsNoTracking().Where(profile => profile.TenantId == tenantId).Select(profile => profile.Currency).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? "USD";
        return new DashboardMetric(Money(currency, total), "Unpaid balance on active memberships");
    }

    private async Task<DashboardMetric> DraftJournalsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Count(await _db.JournalEntries.AsNoTracking().CountAsync(entry => entry.TenantId == tenantId && entry.Status == JournalEntryStatus.Draft, cancellationToken).ConfigureAwait(false), "Draft journal entries");

    private async Task<DashboardMetric> OpenPayrollAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Count(await _db.PayrollRuns.AsNoTracking().CountAsync(run => run.TenantId == tenantId && run.Status != PayrollRunStatus.Paid && run.Status != PayrollRunStatus.PartiallyPaid, cancellationToken).ConfigureAwait(false), "Payroll runs still open");

    private async Task<DashboardMetric> TrainerPerformanceAsync(Guid tenantId, Guid? branchId, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var trainers = _db.TrainerProfiles.AsNoTracking().Where(profile => profile.TenantId == tenantId && profile.Status == ProfileStatus.Active);
        if (branchId is Guid branch)
        {
            trainers = trainers.Where(profile => profile.BranchId == branch);
        }

        var trainerIds = await trainers.Select(profile => profile.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var sessions = trainerIds.Count == 0
            ? 0
            : await _db.PtSessions.AsNoTracking().CountAsync(
                session => session.TenantId == tenantId && trainerIds.Contains(session.TrainerProfileId) && session.Status == PtSessionStatus.Completed && session.CompletedOn == period.Today,
                cancellationToken).ConfigureAwait(false);
        return new DashboardMetric(sessions.ToString(CultureInfo.InvariantCulture), $"{trainerIds.Count.ToString(CultureInfo.InvariantCulture)} active coaches completed sessions today");
    }

    private async Task<DashboardMetric> LeaveQueueAsync(Guid tenantId, Guid? branchId, CancellationToken cancellationToken)
    {
        var rows = _db.StaffLeaveRequests.AsNoTracking().Where(request => request.TenantId == tenantId && request.Status == LeaveRequestStatus.Pending);
        if (branchId is Guid branch)
        {
            rows = rows.Where(request => _db.StaffProfiles.Any(profile => profile.Id == request.StaffProfileId && profile.TenantId == tenantId && profile.BranchId == branch));
        }

        return Count(await rows.CountAsync(cancellationToken).ConfigureAwait(false), "Leave requests waiting for a decision");
    }

    private async Task<DashboardMetric> MyCheckInsAsync(DashboardActorContext context, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var memberId = await _db.MemberProfiles.AsNoTracking()
            .Where(profile => profile.TenantId == context.TenantId && profile.UserId == context.UserId)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (memberId is null)
        {
            return new DashboardMetric("0", "No attendance is recorded for your account.");
        }

        var count = await _db.Attendance.AsNoTracking().CountAsync(
            visit => visit.TenantId == context.TenantId && visit.MemberProfileId == memberId && visit.CheckInTime >= period.TodayStartUtc && visit.CheckInTime < period.TomorrowStartUtc,
            cancellationToken).ConfigureAwait(false);
        return Count(count, "Your check-ins today");
    }

    private async Task<DashboardMetric> TasksAsync(DashboardActorContext context, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
    {
        var pending = await _approvals.CountPendingAsync(context.TenantId, permissions, cancellationToken).ConfigureAwait(false);
        var assigned = await _db.LeadFollowUps.AsNoTracking().CountAsync(item => item.TenantId == context.TenantId && item.CompletedAt == null && item.AssignedUserId == context.UserId, cancellationToken).ConfigureAwait(false);
        return Count(pending + assigned, "Approvals you can decide and follow-ups assigned to you");
    }

    private async Task<DashboardMetric> LeaveStatusAsync(DashboardActorContext context, CancellationToken cancellationToken)
    {
        var latest = await _db.StaffLeaveRequests.AsNoTracking()
            .Where(request => request.TenantId == context.TenantId && request.UserId == context.UserId)
            .OrderByDescending(request => request.SubmittedAt)
            .Select(request => request.Status.ToString())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return new DashboardMetric(latest ?? "None", latest is null ? "You have not submitted a leave request." : "Your latest leave request");
    }

    private async Task<DashboardMetric> UnreadChatsAsync(DashboardActorContext context, CancellationToken cancellationToken)
    {
        var count = await (
            from participant in _db.ChatParticipants.AsNoTracking()
            join message in _db.ChatMessages.AsNoTracking() on participant.ConversationId equals message.ConversationId
            where participant.TenantId == context.TenantId
                && message.TenantId == context.TenantId
                && participant.UserId == context.UserId
                && message.SenderUserId != context.UserId
                && (participant.LastReadAt == null || message.CreatedAt > participant.LastReadAt)
            select message.Id).CountAsync(cancellationToken).ConfigureAwait(false);
        return Count(count, "Unread chat messages");
    }

    private async Task<List<Guid>> AssignedMemberIdsAsync(DashboardActorContext context, CancellationToken cancellationToken)
    {
        if (context.TrainerProfileId is not Guid trainerId)
        {
            return [];
        }

        return await ActiveAssignments(context.TenantId, trainerId).Select(item => item.MemberProfileId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<TrainerMemberAssignment> ActiveAssignments(Guid tenantId, Guid trainerId) =>
        _db.TrainerMemberAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status == TrainerAssignmentStatus.Active);

    private IQueryable<AttendanceRecord> Visits(Guid tenantId, Guid? branchId)
    {
        var visits = _db.Attendance.AsNoTracking().Where(visit => visit.TenantId == tenantId);
        return branchId is Guid branch ? visits.Where(visit => visit.BranchId == branch) : visits;
    }

    private IQueryable<GymMembership> Memberships(Guid tenantId, Guid? branchId)
    {
        var memberships = _db.GymMemberships.AsNoTracking().Where(membership => membership.TenantId == tenantId);
        return branchId is Guid branch ? memberships.Where(membership => membership.BranchId == branch) : memberships;
    }

    private IQueryable<MembershipPayment> PostedPayments(Guid tenantId) =>
        _db.MembershipPayments.AsNoTracking().Where(payment => payment.TenantId == tenantId && payment.Status == MembershipPaymentStatus.Posted);

    private static DashboardMetric Count(int count, string detail) => new(count.ToString(CultureInfo.InvariantCulture), detail);

    private static string Money(string currency, decimal amount) =>
        string.Create(CultureInfo.InvariantCulture, $"{currency} {amount:0.00}");
}
