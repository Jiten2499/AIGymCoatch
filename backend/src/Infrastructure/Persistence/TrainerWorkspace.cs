using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class TrainerWorkspace : ITrainerWorkspace
{
    private readonly ApplicationDbContext _db;

    public TrainerWorkspace(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TrainerListItem>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var trainers = await Profiles(tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var assignments = await ActiveAssignments(tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ptMembers = await ActiveMemberIds(tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = await Names(trainers.Select(item => item.UserId).ToArray(), cancellationToken).ConfigureAwait(false);
        return trainers.Select(trainer =>
        {
            var mine = assignments.Where(item => item.TrainerProfileId == trainer.Id).ToArray();
            return new TrainerListItem(trainer.Id, trainer.UserId, names.GetValueOrDefault(trainer.UserId, "Trainer"), trainer.Status.ToString(), trainer.Specialization, mine.Length, mine.Count(item => ptMembers.Contains(item.MemberProfileId)), trainer.JoiningDate, trainer.EmployeeCode, trainer.CommissionEligible);
        }).ToArray();
    }

    public async Task<IReadOnlyList<TrainerCandidate>> CandidatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var trainers = _db.TrainerProfiles.Where(item => item.TenantId == tenantId).Select(item => item.UserId);
        var rows = await (
            from member in _db.TenantMembers.AsNoTracking()
            join user in _db.Users.AsNoTracking() on member.UserId equals user.Id
            where member.TenantId == tenantId && member.Status == MembershipStatus.Active && !trainers.Contains(user.Id)
            orderby user.FirstName, user.LastName
            select new TrainerCandidate(user.Id, user.FirstName + " " + user.LastName, user.Email)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows;
    }

    public async Task<TrainerProfile?> CreateFromUserAsync(Guid tenantId, Guid userId, string? employeeCode, string? specialization, string? bio, DateOnly? joiningDate, bool commissionEligible, CancellationToken cancellationToken)
    {
        var member = await _db.TenantMembers.AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Status == MembershipStatus.Active, cancellationToken).ConfigureAwait(false);
        if (!member || _db.TrainerProfiles.Local.Any(item => item.TenantId == tenantId && item.UserId == userId) || await _db.TrainerProfiles.AnyAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(employeeCode) && await _db.TrainerProfiles.AnyAsync(item => item.TenantId == tenantId && item.EmployeeCode == employeeCode.Trim(), cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var profile = TrainerProfile.Create(Guid.NewGuid(), tenantId, userId, null);
        profile.Update(bio, specialization, null, null, null, null, ProfileStatus.Active);
        profile.SetEmployment(employeeCode, joiningDate, commissionEligible);
        _db.TrainerProfiles.Add(profile);
        return profile;
    }

    public async Task<bool> UpdateEmploymentAsync(Guid tenantId, Guid trainerId, string? employeeCode, string? specialization, string? bio, DateOnly? joiningDate, bool commissionEligible, ProfileStatus status, CancellationToken cancellationToken)
    {
        var profile = await _db.TrainerProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == trainerId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return false;
        }

        profile.Update(bio, specialization, profile.ExperienceYears, profile.Certification, profile.ProfileImageUrl, profile.BranchId, status);
        profile.SetEmployment(employeeCode, joiningDate, commissionEligible);
        return true;
    }

    public async Task<TrainerDetails?> DetailsAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == trainerId, cancellationToken).ConfigureAwait(false);
        if (trainer is null)
        {
            return null;
        }

        var name = (await Names([trainer.UserId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(trainer.UserId, "Trainer");
        var assignments = await _db.TrainerMemberAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).OrderByDescending(item => item.StartDate).ToListAsync(cancellationToken).ConfigureAwait(false);
        var memberNames = await MemberNames(tenantId, assignments.Select(item => item.MemberProfileId).Distinct().ToArray(), cancellationToken).ConfigureAwait(false);
        var ptMembers = await ActiveMemberIds(tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var active = assignments.Where(item => item.Status == TrainerAssignmentStatus.Active).ToArray();
        var targets = await TargetItems(tenantId, trainerId, from, to, cancellationToken).ConfigureAwait(false);
        var rules = await _db.TrainerIncentiveRules.AsNoTracking().Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new TrainerDetails(
            trainer.Id,
            name,
            trainer.Status.ToString(),
            trainer.Specialization,
            trainer.Bio,
            trainer.EmployeeCode,
            trainer.JoiningDate,
            trainer.CommissionEligible,
            active.Length,
            active.Count(item => ptMembers.Contains(item.MemberProfileId)),
            assignments.Select(item => new TrainerAssignmentItem(item.Id, item.MemberProfileId, memberNames.GetValueOrDefault(item.MemberProfileId, "Member"), item.StartDate, item.EndDate, item.Status.ToString())).ToArray(),
            targets,
            Incentives(targets, rules));
    }

    public async Task<TrainerMemberAssignment?> AssignAsync(Guid tenantId, Guid trainerId, Guid memberId, DateOnly start, Guid assignedBy, CancellationToken cancellationToken)
    {
        if (await _db.TrainerProfiles.AnyAsync(item => item.TenantId == tenantId && item.Id == trainerId && item.Status == ProfileStatus.Active, cancellationToken).ConfigureAwait(false) is false
            || await Member(tenantId, memberId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        return await OpenAssignmentAsync(tenantId, memberId, trainerId, start, assignedBy, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> EndAssignmentAsync(Guid tenantId, Guid assignmentId, DateOnly end, CancellationToken cancellationToken)
    {
        var assignment = await _db.TrainerMemberAssignments.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == assignmentId, cancellationToken).ConfigureAwait(false);
        var member = assignment is null ? null : await Member(tenantId, assignment.MemberProfileId, cancellationToken).ConfigureAwait(false);
        if (assignment is null || member is null)
        {
            return false;
        }

        assignment.End(end);
        if (member.AssignedTrainerId == assignment.TrainerProfileId)
        {
            member.Update(member.DateOfBirth, member.Gender, member.EmergencyContactName, member.EmergencyContactPhone, member.Address, member.City, member.State, member.PostalCode, member.Status, member.EmergencyContactRelationship, member.FitnessGoal, null, member.JoinDate);
        }

        return true;
    }

    public async Task<TrainerMemberAssignment?> ChangeTrainerAsync(Guid tenantId, Guid assignmentId, Guid trainerId, DateOnly start, Guid assignedBy, CancellationToken cancellationToken)
    {
        var current = await _db.TrainerMemberAssignments.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == assignmentId, cancellationToken).ConfigureAwait(false);
        return current is null ? null : await AssignAsync(tenantId, trainerId, current.MemberProfileId, start, assignedBy, cancellationToken).ConfigureAwait(false);
    }

    public Task SyncMemberTrainerAsync(Guid tenantId, Guid memberId, Guid? trainerId, DateOnly start, Guid assignedBy, CancellationToken cancellationToken) =>
        OpenAssignmentAsync(tenantId, memberId, trainerId, start, assignedBy, cancellationToken);

    private async Task<TrainerMemberAssignment?> OpenAssignmentAsync(Guid tenantId, Guid memberId, Guid? trainerId, DateOnly start, Guid assignedBy, CancellationToken cancellationToken)
    {
        var active = await _db.TrainerMemberAssignments.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.MemberProfileId == memberId && item.Status == TrainerAssignmentStatus.Active, cancellationToken).ConfigureAwait(false);
        if (active?.TrainerProfileId == trainerId)
        {
            return active;
        }

        active?.End(start.AddDays(-1));
        var member = await Member(tenantId, memberId, cancellationToken).ConfigureAwait(false);
        if (member is not null && member.AssignedTrainerId != trainerId)
        {
            member.Update(member.DateOfBirth, member.Gender, member.EmergencyContactName, member.EmergencyContactPhone, member.Address, member.City, member.State, member.PostalCode, member.Status, member.EmergencyContactRelationship, member.FitnessGoal, trainerId, member.JoinDate);
        }

        if (trainerId is not Guid next || !await _db.TrainerProfiles.AnyAsync(item => item.TenantId == tenantId && item.Id == next, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var opened = TrainerMemberAssignment.Start(Guid.NewGuid(), tenantId, next, memberId, start, assignedBy);
        _db.TrainerMemberAssignments.Add(opened);
        return opened;
    }

    public Task<IReadOnlyList<TrainerTargetItem>> TargetsAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        TargetItems(tenantId, trainerId, from, to, cancellationToken);

    public async Task<TrainerTarget?> AddTargetAsync(Guid tenantId, Guid trainerId, TrainerTargetType type, TrainerTargetPeriod period, DateOnly start, DateOnly end, decimal value, CancellationToken cancellationToken, string? name = null)
    {
        if (value <= 0 || !await _db.TrainerProfiles.AnyAsync(item => item.TenantId == tenantId && item.Id == trainerId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var range = TrainerPerformanceMath.PeriodRange(period, start, end);
        var target = TrainerTarget.Create(Guid.NewGuid(), tenantId, trainerId, type, period, range.Start, range.End, value, name);
        _db.TrainerTargets.Add(target);
        return target;
    }

    public async Task SaveIncentiveRuleAsync(Guid tenantId, TrainerTargetType type, decimal percent, CancellationToken cancellationToken)
    {
        var rule = await _db.TrainerIncentiveRules.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.TargetType == type, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            _db.TrainerIncentiveRules.Add(TrainerIncentiveRule.Create(Guid.NewGuid(), tenantId, type, percent));
            return;
        }

        rule.ChangePercent(percent);
    }

    public async Task<IReadOnlyList<TrainerPerformanceRow>> PerformanceAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var trainers = await Profiles(tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = await Names(trainers.Select(item => item.UserId).ToArray(), cancellationToken).ConfigureAwait(false);
        var started = await _db.TrainerMemberAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.StartDate >= from && item.StartDate <= to).ToListAsync(cancellationToken).ConfigureAwait(false);
        var sessions = await _db.PtSessions.AsNoTracking().Where(item => item.TenantId == tenantId && item.Status == PtSessionStatus.Completed && item.CompletedOn >= from && item.CompletedOn <= to).ToListAsync(cancellationToken).ConfigureAwait(false);
        var targets = await _db.TrainerTargets.AsNoTracking().Where(item => item.TenantId == tenantId && item.IsActive && item.PeriodStart <= to && item.PeriodEnd >= from).ToListAsync(cancellationToken).ConfigureAwait(false);
        return trainers.Select(trainer =>
        {
            var clients = started.Count(item => item.TrainerProfileId == trainer.Id);
            var completed = sessions.Count(item => item.TrainerProfileId == trainer.Id);
            var mine = targets.Where(item => item.TrainerProfileId == trainer.Id).Select(item => TrainerPerformanceMath.Achievement(Actual(item.TargetType, clients, completed), item.TargetValue)).Where(item => item is not null).Select(item => item!.Value).ToArray();
            return new TrainerPerformanceRow(trainer.Id, names.GetValueOrDefault(trainer.UserId, "Trainer"), null, completed, clients, mine.Length == 0 ? null : Math.Round(mine.Average(), 2, MidpointRounding.AwayFromZero));
        }).ToArray();
    }

    private IQueryable<TrainerProfile> Profiles(Guid tenantId) =>
        _db.TrainerProfiles.AsNoTracking().Where(item => item.TenantId == tenantId).OrderBy(item => item.JoiningDate);

    private IQueryable<TrainerMemberAssignment> ActiveAssignments(Guid tenantId) =>
        _db.TrainerMemberAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.Status == TrainerAssignmentStatus.Active);

    private IQueryable<Guid> ActiveMemberIds(Guid tenantId) =>
        _db.GymMemberships.AsNoTracking().Where(item => item.TenantId == tenantId && item.Status == GymMembershipStatus.Active).Select(item => item.MemberProfileId).Distinct();

    private async Task<Dictionary<Guid, string>> Names(Guid[] userIds, CancellationToken cancellationToken) =>
        await _db.Users.AsNoTracking().Where(item => userIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.FirstName + " " + item.LastName, cancellationToken).ConfigureAwait(false);

    private async Task<Dictionary<Guid, string>> MemberNames(Guid tenantId, Guid[] memberIds, CancellationToken cancellationToken) =>
        await _db.MemberProfiles.AsNoTracking().Where(item => item.TenantId == tenantId && memberIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.FirstName + " " + item.LastName, cancellationToken).ConfigureAwait(false);

    private Task<MemberProfile?> Member(Guid tenantId, Guid memberId, CancellationToken cancellationToken) =>
        _db.MemberProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == memberId, cancellationToken);

    private async Task<IReadOnlyList<TrainerTargetItem>> TargetItems(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var targets = await _db.TrainerTargets.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.PeriodStart <= to && item.PeriodEnd >= from).ToListAsync(cancellationToken).ConfigureAwait(false);
        var clients = await _db.TrainerMemberAssignments.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.StartDate >= from && item.StartDate <= to, cancellationToken).ConfigureAwait(false);
        var sessions = await _db.PtSessions.CountAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status == PtSessionStatus.Completed && item.CompletedOn >= from && item.CompletedOn <= to, cancellationToken).ConfigureAwait(false);
        return targets.Select(item =>
        {
            var actual = Actual(item.TargetType, clients, sessions);
            return new TrainerTargetItem(item.Id, item.TargetType.ToString(), item.Period.ToString(), item.PeriodStart, item.PeriodEnd, item.TargetValue, actual, TrainerPerformanceMath.Achievement(actual, item.TargetValue), item.Name, item.IsActive);
        }).ToArray();
    }

    private static decimal? Actual(TrainerTargetType type, int newClients, int sessions) => type switch
    {
        TrainerTargetType.NewPtClients => newClients,
        TrainerTargetType.PtSessions => sessions,
        _ => null,
    };

    private static IReadOnlyList<TrainerIncentiveItem> Incentives(IReadOnlyList<TrainerTargetItem> targets, IReadOnlyList<TrainerIncentiveRule> rules) =>
        rules.Select(rule =>
        {
            var target = targets.FirstOrDefault(item => item.TargetType == rule.TargetType.ToString());
            var achieved = target is not null && target.Actual is not null && target.Actual >= target.TargetValue;
            var amount = target?.Actual is null || rule.TargetType != TrainerTargetType.PtRevenue ? null : TrainerPerformanceMath.IncentiveAmount(target.Actual.Value, target.TargetValue, rule.Percent);
            var status = target is null ? "No target" : target.Actual is null ? "Unavailable" : achieved ? "Achieved" : "Not achieved";
            return new TrainerIncentiveItem(rule.TargetType.ToString(), rule.Percent, status, amount);
        }).ToArray();
}
