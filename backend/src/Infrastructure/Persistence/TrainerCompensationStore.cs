using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class TrainerWorkspace
{
    public async Task<bool> SetTargetActiveAsync(Guid tenantId, Guid targetId, bool active, CancellationToken cancellationToken)
    {
        var target = await _db.TrainerTargets.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == targetId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return false;
        }

        target.SetActive(active);
        return true;
    }

    public async Task<TrainerCommissionRule?> AddCommissionRuleAsync(Guid tenantId, Guid trainerId, string name, CommissionRuleKind kind, decimal? percent, decimal? fixedAmount, DateOnly start, DateOnly? end, bool requiresApproval, CancellationToken cancellationToken)
    {
        if (!ValidRule(name, start, end, kind == CommissionRuleKind.PercentOfEligiblePtRevenue ? percent : fixedAmount) || !await OwnsTrainer(tenantId, trainerId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var rule = TrainerCommissionRule.Create(Guid.NewGuid(), tenantId, trainerId, name, kind, percent, fixedAmount, start, end, requiresApproval);
        _db.TrainerCommissionRules.Add(rule);
        return rule;
    }

    public async Task<TrainerPerformanceIncentive?> AddPerformanceIncentiveAsync(Guid tenantId, Guid trainerId, string name, TrainerTargetType metric, decimal threshold, IncentiveRewardKind rewardKind, decimal? fixedAmount, decimal? percent, DateOnly start, DateOnly? end, bool requiresApproval, CancellationToken cancellationToken)
    {
        var amount = rewardKind == IncentiveRewardKind.FixedOnAchievement ? fixedAmount : percent;
        if (threshold <= 0 || !ValidRule(name, start, end, amount) || !await OwnsTrainer(tenantId, trainerId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var rule = TrainerPerformanceIncentive.Create(Guid.NewGuid(), tenantId, trainerId, name, metric, threshold, rewardKind, fixedAmount, percent, start, end, requiresApproval);
        _db.TrainerPerformanceIncentives.Add(rule);
        return rule;
    }

    public async Task<int> CalculateCompensationAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (!await OwnsTrainer(tenantId, trainerId, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var clients = await _db.TrainerMemberAssignments.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.StartDate >= from && item.StartDate <= to, cancellationToken).ConfigureAwait(false);
        var packagesSold = await _db.MemberPtAssignments.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status != MemberPtAssignmentStatus.Cancelled && item.StartDate >= from && item.StartDate <= to, cancellationToken).ConfigureAwait(false);
        var sessions = await _db.PtSessions.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status == PtSessionStatus.Completed && item.CompletedOn >= from && item.CompletedOn <= to, cancellationToken).ConfigureAwait(false);
        var created = 0;
        var commissions = await _db.TrainerCommissionRules.Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in commissions.Where(item => item.Covers(from)))
        {
            var amount = CoachCompensationMath.Commission(rule.Kind, rule.Percent, rule.FixedAmount, null, packagesSold);
            if (amount is null || !await OpenCalculation(tenantId, trainerId, rule.Id, from, to, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            _db.TrainerCompensationCalculations.Add(TrainerCompensationCalculation.Create(Guid.NewGuid(), tenantId, trainerId, CompensationKind.Commission, rule.Id, from, to, amount.Value, rule.Percent is not null ? "Eligible PT revenue is not recorded." : $"Standalone PT packages sold {packagesSold}."));
            created++;
        }

        var incentives = await _db.TrainerPerformanceIncentives.Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in incentives.Where(item => item.Covers(from)))
        {
            decimal? actual = rule.Metric switch
            {
                TrainerTargetType.NewPtClients => clients,
                TrainerTargetType.PtSessions => sessions,
                _ => null,
            };
            var amount = CoachCompensationMath.Incentive(rule.RewardKind, actual, rule.Threshold, rule.FixedAmount, rule.Percent, null);
            if (amount is null || !await OpenCalculation(tenantId, trainerId, rule.Id, from, to, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var basis = actual is null ? "Source metric is unavailable." : $"{rule.Metric} {actual} against {rule.Threshold}.";
            _db.TrainerCompensationCalculations.Add(TrainerCompensationCalculation.Create(Guid.NewGuid(), tenantId, trainerId, CompensationKind.Incentive, rule.Id, from, to, amount.Value, basis));
            created++;
        }

        return created;
    }

    public async Task<TrainerCompensationCalculation?> DecideCalculationAsync(Guid tenantId, Guid calculationId, CompensationStatus status, CancellationToken cancellationToken)
    {
        var row = await _db.TrainerCompensationCalculations.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == calculationId, cancellationToken).ConfigureAwait(false);
        return row is not null && row.MoveTo(status) ? row : null;
    }

    public async Task<CoachWorkspace?> CoachAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == trainerId, cancellationToken).ConfigureAwait(false);
        if (trainer is null)
        {
            return null;
        }

        var user = await _db.Users.AsNoTracking().FirstAsync(item => item.Id == trainer.UserId, cancellationToken).ConfigureAwait(false);
        var assignments = await _db.TrainerMemberAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).OrderByDescending(item => item.StartDate).ToListAsync(cancellationToken).ConfigureAwait(false);
        var memberIds = assignments.Select(item => item.MemberProfileId).Distinct().ToArray();
        var members = await _db.MemberProfiles.AsNoTracking().Where(item => item.TenantId == tenantId && memberIds.Contains(item.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var memberships = await _db.GymMemberships.AsNoTracking().Where(item => item.TenantId == tenantId && memberIds.Contains(item.MemberProfileId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ptMembers = await ActiveMemberIds(tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var targets = await TargetItems(tenantId, trainerId, from, to, cancellationToken).ConfigureAwait(false);
        var activeTargets = targets.Where(item => item.IsActive && item.AchievementPercent is not null).Select(item => item.AchievementPercent!.Value).ToArray();
        var calculations = await _db.TrainerCompensationCalculations.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.PeriodStart <= to && item.PeriodEnd >= from).ToListAsync(cancellationToken).ConfigureAwait(false);
        var commissionRules = await _db.TrainerCommissionRules.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var incentiveRules = await _db.TrainerPerformanceIncentives.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = commissionRules.Select(item => (item.Id, item.Name)).Concat(incentiveRules.Select(item => (item.Id, item.Name))).ToDictionary(item => item.Id, item => item.Name);
        var active = assignments.Where(item => item.Status == TrainerAssignmentStatus.Active).ToArray();
        var clients = await _db.TrainerMemberAssignments.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.StartDate >= from && item.StartDate <= to, cancellationToken).ConfigureAwait(false);
        var packagesSold = await _db.MemberPtAssignments.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status != MemberPtAssignmentStatus.Cancelled && item.StartDate >= from && item.StartDate <= to, cancellationToken).ConfigureAwait(false);
        var sessions = await _db.PtSessions.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status == PtSessionStatus.Completed && item.CompletedOn >= from && item.CompletedOn <= to, cancellationToken).ConfigureAwait(false);
        return new CoachWorkspace(
            trainer.Id,
            user.FirstName + " " + user.LastName,
            user.Email,
            trainer.Status.ToString(),
            trainer.Specialization,
            trainer.JoiningDate,
            trainer.EmployeeCode,
            active.Length,
            active.Count(item => ptMembers.Contains(item.MemberProfileId)),
            clients,
            activeTargets.Length == 0 ? null : Math.Round(activeTargets.Average(), 2, MidpointRounding.AwayFromZero),
            Sum(calculations, CompensationKind.Commission, CompensationStatus.Calculated),
            Sum(calculations, CompensationKind.Incentive, CompensationStatus.Calculated),
            calculations.Where(item => item.Status == CompensationStatus.PendingApproval).Sum(item => item.Amount),
            calculations.Where(item => item.Status == CompensationStatus.Approved).Sum(item => item.Amount),
            assignments.Select(item => MapAssignment(item, members, memberships)).ToArray(),
            targets,
            commissionRules.Select(item => new CoachRuleRow(item.Id, item.Name, item.Kind.ToString(), item.Percent is null ? $"{item.FixedAmount} per package" : $"{item.Percent}% of eligible PT revenue", item.EffectiveStart, item.EffectiveEnd, item.IsActive, item.RequiresApproval)).ToArray(),
            incentiveRules.Select(item => new CoachRuleRow(item.Id, item.Name, item.RewardKind.ToString(), $"{item.Metric} at {item.Threshold}", item.EffectiveStart, item.EffectiveEnd, item.IsActive, item.RequiresApproval)).ToArray(),
            calculations.Select(item => new CoachCalculationRow(item.Id, item.Kind.ToString(), item.RuleId, names.GetValueOrDefault(item.RuleId, "Rule"), item.PeriodStart, item.PeriodEnd, item.Amount, item.Status.ToString(), item.Basis)).ToArray(),
            await History(tenantId, trainerId, assignments, cancellationToken).ConfigureAwait(false),
            sessions,
            packagesSold);
    }

    private async Task<bool> OpenCalculation(Guid tenantId, Guid trainerId, Guid ruleId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var open = new[] { CompensationStatus.Calculated, CompensationStatus.PendingApproval, CompensationStatus.Approved };
        if (_db.TrainerCompensationCalculations.Local.Any(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.RuleId == ruleId && item.PeriodStart == from && item.PeriodEnd == to && open.Contains(item.Status)))
        {
            return false;
        }

        return !await _db.TrainerCompensationCalculations.AnyAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.RuleId == ruleId && item.PeriodStart == from && item.PeriodEnd == to && open.Contains(item.Status), cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> OwnsTrainer(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        _db.TrainerProfiles.AnyAsync(item => item.TenantId == tenantId && item.Id == trainerId, cancellationToken);

    private static bool ValidRule(string name, DateOnly start, DateOnly? end, decimal? amount) =>
        !string.IsNullOrWhiteSpace(name) && amount is > 0 && (end is null || end >= start);

    private static decimal Sum(IEnumerable<TrainerCompensationCalculation> rows, CompensationKind kind, CompensationStatus status) =>
        rows.Where(item => item.Kind == kind && item.Status == status).Sum(item => item.Amount);

    private static CoachAssignmentRow MapAssignment(TrainerMemberAssignment item, IReadOnlyList<MemberProfile> members, IReadOnlyList<GymMembership> memberships)
    {
        var member = members.FirstOrDefault(row => row.Id == item.MemberProfileId);
        var membership = memberships.Where(row => row.MemberProfileId == item.MemberProfileId).OrderByDescending(row => row.EndDate).FirstOrDefault();
        return new CoachAssignmentRow(item.Id, item.MemberProfileId, member is null ? "Member" : member.FirstName + " " + member.LastName, member?.Phone, item.StartDate, item.EndDate, item.Status.ToString(), membership?.Status.ToString(), membership?.EndDate);
    }
}
