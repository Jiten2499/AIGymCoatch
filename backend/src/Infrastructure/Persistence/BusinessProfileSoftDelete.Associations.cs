using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class BusinessProfileSoftDelete
{
    private enum BusinessProfileKind
    {
        Member,
        Trainer,
        Staff,
    }

    private async Task DeactivateOperationsAsync(BusinessProfileKind kind, Guid tenantId, MemberProfile? member, TrainerProfile? trainer, StaffProfile? staff, DateTime utcNow, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(utcNow);
        if (kind == BusinessProfileKind.Member && member is not null)
        {
            var memberships = await _db.GymMemberships
                .Where(item => item.TenantId == tenantId && item.MemberProfileId == member.Id && (item.Status == GymMembershipStatus.Active || item.Status == GymMembershipStatus.Pending || item.Status == GymMembershipStatus.Frozen))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var membership in memberships)
            {
                membership.Cancel();
            }

            var packages = await _db.MemberPtAssignments
                .Where(item => item.TenantId == tenantId && item.MemberProfileId == member.Id && (item.Status == MemberPtAssignmentStatus.Active || item.Status == MemberPtAssignmentStatus.PendingPayment))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var package in packages)
            {
                package.Cancel(utcNow, "Member profile deleted");
            }

            await CancelScheduledSessionsAsync(tenantId, member.Id, null, cancellationToken).ConfigureAwait(false);
            await EndAssignmentsAsync(tenantId, null, member.Id, today, cancellationToken).ConfigureAwait(false);
        }

        if (kind == BusinessProfileKind.Trainer && trainer is not null)
        {
            await EndAssignmentsAsync(tenantId, trainer.Id, null, today, cancellationToken).ConfigureAwait(false);
            var members = await _db.MemberProfiles.Where(item => item.TenantId == tenantId && item.AssignedTrainerId == trainer.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var assigned in members)
            {
                assigned.ClearAssignedTrainer();
            }

            var targets = await _db.TrainerTargets.Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainer.Id && item.IsActive).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var target in targets)
            {
                target.SetActive(false);
            }

            await CancelScheduledSessionsAsync(tenantId, null, trainer.Id, cancellationToken).ConfigureAwait(false);
        }

        if (kind == BusinessProfileKind.Staff && staff is not null)
        {
            var access = await _db.StaffBranchAccess.Where(item => item.TenantId == tenantId && item.StaffProfileId == staff.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var row in access)
            {
                row.SoftDelete(utcNow);
            }

            var leave = await _db.StaffLeaveRequests.Where(item => item.TenantId == tenantId && item.StaffProfileId == staff.Id && item.Status == LeaveRequestStatus.Pending).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var request in leave)
            {
                request.Cancel(utcNow);
            }
        }
    }

    private async Task RevokeAccessAsync(BusinessProfileKind kind, Guid tenantId, Guid? userId, Guid? memberProfileId, Guid? trainerProfileId, Guid? staffProfileId, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (userId is not Guid personId)
        {
            return;
        }

        var staffQuery = _db.StaffProfiles.Where(item => item.TenantId == tenantId && item.UserId == personId);
        var trainerQuery = _db.TrainerProfiles.Where(item => item.TenantId == tenantId && item.UserId == personId);
        var memberQuery = _db.MemberProfiles.Where(item => item.TenantId == tenantId && item.UserId == personId);
        if (staffProfileId is Guid excludedStaff)
        {
            staffQuery = staffQuery.Where(item => item.Id != excludedStaff);
        }

        if (trainerProfileId is Guid excludedTrainer)
        {
            trainerQuery = trainerQuery.Where(item => item.Id != excludedTrainer);
        }

        if (memberProfileId is Guid excludedMember)
        {
            memberQuery = memberQuery.Where(item => item.Id != excludedMember);
        }

        var staffRemains = await staffQuery.AnyAsync(cancellationToken).ConfigureAwait(false);
        var trainerRemains = await trainerQuery.AnyAsync(cancellationToken).ConfigureAwait(false);
        var memberRemains = await memberQuery.AnyAsync(cancellationToken).ConfigureAwait(false);
        var assignments = await (
            from assignment in _db.UserRoles
            join role in _db.Roles on assignment.RoleId equals role.Id
            where assignment.TenantId == tenantId && assignment.UserId == personId
            select new { assignment, role.Name }).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in assignments.Where(item => !KeepRole(kind, item.Name, staffRemains, trainerRemains, memberRemains)))
        {
            item.assignment.Remove(utcNow);
        }

        if (!staffRemains && !trainerRemains && !memberRemains)
        {
            var membership = await _db.TenantMembers.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == personId && item.Status == MembershipStatus.Active, cancellationToken).ConfigureAwait(false);
            membership?.Remove(utcNow);

            var grants = await _db.UserPermissionOverrides.Where(item => item.TenantId == tenantId && item.UserId == personId).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var grant in grants)
            {
                grant.SoftDelete(utcNow);
            }

            var scopes = await _db.UserResourceScopes.Where(item => item.TenantId == tenantId && item.UserId == personId).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var scope in scopes)
            {
                scope.SoftDelete(utcNow);
            }

            var participants = await _db.ChatParticipants.Where(item => item.TenantId == tenantId && item.UserId == personId).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var participant in participants)
            {
                participant.SoftDelete(utcNow);
            }
        }

        _permissions.Schedule(tenantId, personId);
    }

    private static bool KeepRole(BusinessProfileKind kind, string roleName, bool staffRemains, bool trainerRemains, bool memberRemains)
    {
        var trainerRole = roleName == RoleNames.Trainer;
        var memberRole = roleName == RoleNames.Member;
        return kind switch
        {
            BusinessProfileKind.Staff => (trainerRole && trainerRemains) || (memberRole && memberRemains),
            BusinessProfileKind.Trainer => !trainerRole && (staffRemains || (memberRole && memberRemains)),
            _ => !memberRole && (staffRemains || (trainerRole && trainerRemains)),
        };
    }

    private async Task EndAssignmentsAsync(Guid tenantId, Guid? trainerId, Guid? memberId, DateOnly today, CancellationToken cancellationToken)
    {
        var query = _db.TrainerMemberAssignments.Where(item => item.TenantId == tenantId && item.Status == TrainerAssignmentStatus.Active);
        if (trainerId is Guid trainer)
        {
            query = query.Where(item => item.TrainerProfileId == trainer);
        }

        if (memberId is Guid member)
        {
            query = query.Where(item => item.MemberProfileId == member);
        }

        var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            row.End(today);
        }
    }

    private async Task CancelScheduledSessionsAsync(Guid tenantId, Guid? memberId, Guid? trainerId, CancellationToken cancellationToken)
    {
        var query = _db.PtSessions.Where(item => item.TenantId == tenantId && item.Status == PtSessionStatus.Scheduled);
        if (memberId is Guid member)
        {
            query = query.Where(item => item.MemberProfileId == member);
        }

        if (trainerId is Guid trainer)
        {
            query = query.Where(item => item.TrainerProfileId == trainer);
        }

        var sessions = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var session in sessions)
        {
            session.Cancel();
        }
    }
}
