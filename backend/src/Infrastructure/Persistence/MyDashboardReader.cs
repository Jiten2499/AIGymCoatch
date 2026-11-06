using System.Globalization;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class MyDashboardReader : IMyDashboardReader
{
    private readonly ApplicationDbContext _db;
    private readonly ICommunicationRepository _communications;
    private readonly IChallengeRepository _challenges;
    private readonly IApprovalRequestRepository _approvals;

    public MyDashboardReader(
        ApplicationDbContext db,
        ICommunicationRepository communications,
        IChallengeRepository challenges,
        IApprovalRequestRepository approvals)
    {
        _db = db;
        _communications = communications;
        _challenges = challenges;
        _approvals = approvals;
    }

    public async Task<DashboardActorContext> DescribeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var trainerId = await _db.TrainerProfiles.AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var branchId = await _db.StaffProfiles.AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.UserId == userId)
            .Select(profile => profile.BranchId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        branchId ??= await _db.TrainerProfiles.AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.UserId == userId)
            .Select(profile => profile.BranchId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var paymentScopes = await (
            from assignment in _db.UserRoles.AsNoTracking()
            join scope in _db.RoleResourceScopes.AsNoTracking() on assignment.RoleId equals scope.RoleId
            where assignment.TenantId == tenantId && assignment.UserId == userId && scope.Resource == "payments"
            select scope.Scope).ToListAsync(cancellationToken).ConfigureAwait(false);
        var currency = await _db.BusinessProfiles.AsNoTracking()
            .Where(profile => profile.TenantId == tenantId)
            .Select(profile => profile.Currency)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return new DashboardActorContext(tenantId, userId, trainerId, branchId, MyDashboardCatalog.PaymentsAreShared(paymentScopes), string.IsNullOrWhiteSpace(currency) ? "USD" : currency);
    }

    public async Task<IReadOnlyDictionary<string, DashboardMetric>> ReadAsync(
        DashboardActorContext context,
        bool limitToBranch,
        bool followUpsAll,
        IReadOnlyList<string> permissions,
        IReadOnlyCollection<string> widgetCodes,
        DateTime utcNow,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var period = DashboardPeriod.For(utcNow, timeZone);
        var branchId = limitToBranch ? context.BranchId : null;
        var metrics = new Dictionary<string, DashboardMetric>(StringComparer.Ordinal);
        foreach (var code in widgetCodes.Distinct(StringComparer.Ordinal))
        {
            metrics[code] = await ReadOneAsync(code, context, branchId, followUpsAll, permissions, period, utcNow, cancellationToken).ConfigureAwait(false);
        }

        return metrics;
    }

    private async Task<DashboardMetric> ReadOneAsync(
        string code,
        DashboardActorContext context,
        Guid? branchId,
        bool followUpsAll,
        IReadOnlyList<string> permissions,
        DashboardPeriod period,
        DateTime utcNow,
        CancellationToken cancellationToken) => code switch
    {
        "checkins" or "operations" or "attendance" => await CheckInsAsync(context.TenantId, branchId, period, cancellationToken).ConfigureAwait(false),
        "new-members" => await NewMembersAsync(context.TenantId, branchId, period, cancellationToken).ConfigureAwait(false),
        "expiring" or "renewals" => await ExpiringAsync(context.TenantId, branchId, period, cancellationToken).ConfigureAwait(false),
        "collections" => await CollectionsAsync(context, period, false, cancellationToken).ConfigureAwait(false),
        "financials" => await CollectionsAsync(context, period, true, cancellationToken).ConfigureAwait(false),
        "follow-ups" => await FollowUpsAsync(context, followUpsAll, period, cancellationToken).ConfigureAwait(false),
        "sessions" or "schedule" => await SessionsAsync(context, period, cancellationToken).ConfigureAwait(false),
        "assigned-members" => await AssignedMembersAsync(context, cancellationToken).ConfigureAwait(false),
        "assessments" => await AssessmentsAsync(context, period, cancellationToken).ConfigureAwait(false),
        "progress" => await ProgressAsync(context, period, cancellationToken).ConfigureAwait(false),
        "pt-targets" => await TargetsAsync(context, period, cancellationToken).ConfigureAwait(false),
        "coach-credits" => await CreditsAsync(context, cancellationToken).ConfigureAwait(false),
        "outstanding" => await OutstandingAsync(context.TenantId, branchId, cancellationToken).ConfigureAwait(false),
        "reconciliation" => await DraftJournalsAsync(context.TenantId, cancellationToken).ConfigureAwait(false),
        "payroll" => await OpenPayrollAsync(context.TenantId, cancellationToken).ConfigureAwait(false),
        "trainer-performance" => await TrainerPerformanceAsync(context.TenantId, branchId, period, cancellationToken).ConfigureAwait(false),
        "leave-requests" => await LeaveQueueAsync(context.TenantId, branchId, cancellationToken).ConfigureAwait(false),
        "my-attendance" => await MyCheckInsAsync(context, period, cancellationToken).ConfigureAwait(false),
        "tasks" => await TasksAsync(context, permissions, cancellationToken).ConfigureAwait(false),
        "leave-status" => await LeaveStatusAsync(context, cancellationToken).ConfigureAwait(false),
        "circulars" => Count(await _communications.CountUnreadAsync(context.TenantId, context.UserId, utcNow, cancellationToken).ConfigureAwait(false), "Unread circulars"),
        "notifications" => Count(await _challenges.CountUnreadNotificationsAsync(context.TenantId, context.UserId, cancellationToken).ConfigureAwait(false), "Unread notifications"),
        "chats" => await UnreadChatsAsync(context, cancellationToken).ConfigureAwait(false),
        _ => new DashboardMetric("0", "No figures for this period."),
    };

    private async Task<DashboardMetric> CheckInsAsync(Guid tenantId, Guid? branchId, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var count = await Visits(tenantId, branchId).CountAsync(visit => visit.CheckInTime >= period.TodayStartUtc && visit.CheckInTime < period.TomorrowStartUtc, cancellationToken).ConfigureAwait(false);
        return Count(count, "Check-ins recorded today");
    }

    private async Task<DashboardMetric> NewMembersAsync(Guid tenantId, Guid? branchId, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var members = _db.MemberProfiles.AsNoTracking().Where(profile => profile.TenantId == tenantId && profile.Status == ProfileStatus.Active && profile.JoinDate == period.Today);
        if (branchId is Guid branch)
        {
            members = members.Where(profile => _db.GymMemberships.Any(membership => membership.TenantId == tenantId && membership.MemberProfileId == profile.Id && membership.BranchId == branch));
        }

        return Count(await members.CountAsync(cancellationToken).ConfigureAwait(false), "Members who joined today");
    }

    private async Task<DashboardMetric> ExpiringAsync(Guid tenantId, Guid? branchId, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var count = await Memberships(tenantId, branchId).CountAsync(
            membership => membership.Status == GymMembershipStatus.Active && membership.EndDate >= period.Today && membership.EndDate <= period.ExpiringThrough7Days,
            cancellationToken).ConfigureAwait(false);
        return Count(count, "Active memberships ending within 7 days");
    }

    private async Task<DashboardMetric> CollectionsAsync(DashboardActorContext context, DashboardPeriod period, bool month, CancellationToken cancellationToken)
    {
        var payments = PostedPayments(context.TenantId);
        if (!month && !context.PaymentsAll)
        {
            payments = payments.Where(payment => payment.ReceivedByUserId == context.UserId);
        }

        var from = month ? period.MonthStart : period.Today;
        var to = period.Today;
        var total = await payments.Where(payment => payment.PaymentDate >= from && payment.PaymentDate <= to).SumAsync(payment => (decimal?)payment.Amount, cancellationToken).ConfigureAwait(false) ?? 0m;
        return new DashboardMetric(Money(context.Currency, total), month ? "Posted collections this month" : context.PaymentsAll ? "Posted collections today" : "Collections you received today");
    }

    private async Task<DashboardMetric> FollowUpsAsync(DashboardActorContext context, bool followUpsAll, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var rows = _db.LeadFollowUps.AsNoTracking().Where(item => item.TenantId == context.TenantId && item.CompletedAt == null && item.ScheduledAt >= period.TodayStartUtc && item.ScheduledAt < period.TomorrowStartUtc);
        if (!followUpsAll)
        {
            rows = rows.Where(item => item.AssignedUserId == context.UserId);
        }

        return Count(await rows.CountAsync(cancellationToken).ConfigureAwait(false), followUpsAll ? "Open follow-ups due today" : "Follow-ups assigned to you today");
    }
}
