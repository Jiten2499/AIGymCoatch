using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class BusinessProfileSoftDelete : IBusinessProfileSoftDelete
{
    private readonly ApplicationDbContext _db;
    private readonly IPermissionCacheInvalidator _permissions;

    public BusinessProfileSoftDelete(ApplicationDbContext db, IPermissionCacheInvalidator permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<Result> DeleteMemberAsync(Guid tenantId, Guid actorId, Guid memberProfileId, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken).ConfigureAwait(false);
        var member = await _db.MemberProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == memberProfileId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || member is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        if (member.UserId is Guid userId)
        {
            var blocked = await GuardPersonAsync(tenant, actorId, userId, cancellationToken).ConfigureAwait(false);
            if (blocked.IsFailure)
            {
                return blocked;
            }
        }

        member.Remove(utcNow);
        await DeactivateOperationsAsync(BusinessProfileKind.Member, tenantId, member, null, null, utcNow, cancellationToken).ConfigureAwait(false);
        await RevokeAccessAsync(BusinessProfileKind.Member, tenantId, member.UserId, member.Id, null, null, utcNow, cancellationToken).ConfigureAwait(false);
        await AuditAsync(tenantId, actorId, "DeleteMemberProfile", "MemberProfile", member.Id, member.MemberCode, utcNow, ipAddress, userAgent, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> DeleteTrainerAsync(Guid tenantId, Guid actorId, Guid trainerProfileId, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken).ConfigureAwait(false);
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == trainerProfileId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || trainer is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        var blocked = await GuardPersonAsync(tenant, actorId, trainer.UserId, cancellationToken).ConfigureAwait(false);
        if (blocked.IsFailure)
        {
            return blocked;
        }

        trainer.Remove(utcNow);
        await DeactivateOperationsAsync(BusinessProfileKind.Trainer, tenantId, null, trainer, null, utcNow, cancellationToken).ConfigureAwait(false);
        await RevokeAccessAsync(BusinessProfileKind.Trainer, tenantId, trainer.UserId, null, trainer.Id, null, utcNow, cancellationToken).ConfigureAwait(false);
        await AuditAsync(tenantId, actorId, "DeleteTrainerProfile", "TrainerProfile", trainer.Id, trainer.EmployeeCode, utcNow, ipAddress, userAgent, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> DeleteStaffAsync(Guid tenantId, Guid actorId, Guid staffProfileId, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var staff = await _db.StaffProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == staffProfileId, cancellationToken).ConfigureAwait(false);
        if (staff is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        return await DeleteStaffUsersAsync(tenantId, actorId, [staff.UserId], utcNow, ipAddress, userAgent, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteStaffUsersAsync(Guid tenantId, Guid actorId, IReadOnlyList<Guid> userIds, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        var ids = userIds.Distinct().ToArray();
        var staff = new List<StaffProfile>();
        foreach (var userId in ids)
        {
            var blocked = await GuardPersonAsync(tenant, actorId, userId, cancellationToken).ConfigureAwait(false);
            if (blocked.IsFailure)
            {
                return blocked;
            }

            var profile = await _db.StaffProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken).ConfigureAwait(false);
            var membership = await _db.TenantMembers.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Status == MembershipStatus.Active, cancellationToken).ConfigureAwait(false);
            if (profile is null && membership is null)
            {
                return Result.Failure(IdentityErrors.MemberNotFound);
            }

            if (profile is not null)
            {
                staff.Add(profile);
            }
        }

        foreach (var profile in staff)
        {
            profile.Remove(utcNow);
            await DeactivateOperationsAsync(BusinessProfileKind.Staff, tenantId, null, null, profile, utcNow, cancellationToken).ConfigureAwait(false);
        }

        foreach (var userId in ids)
        {
            var profile = staff.FirstOrDefault(item => item.UserId == userId);
            await RevokeAccessAsync(BusinessProfileKind.Staff, tenantId, userId, null, null, profile?.Id, utcNow, cancellationToken).ConfigureAwait(false);
            await AuditAsync(tenantId, actorId, "DeleteStaffProfile", profile is null ? "TenantMember" : "StaffProfile", profile?.Id ?? userId, profile?.EmployeeCode, utcNow, ipAddress, userAgent, cancellationToken).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> GuardPersonAsync(Tenant tenant, Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        if (tenant.OwnerUserId == userId)
        {
            return Result.Failure(IdentityErrors.OwnerRemovalRejected);
        }

        if (actorId == userId)
        {
            return Result.Failure(IdentityErrors.SelfRemovalRejected);
        }

        await SeatAssignmentLock.AcquireAsync(_db, tenant.Id, userId, cancellationToken).ConfigureAwait(false);
        var seated = await (
            from seat in _db.SubscriptionSeats
            join subscription in _db.Subscriptions on seat.SubscriptionId equals subscription.Id
            where subscription.TenantId == tenant.Id && seat.UserId == userId && seat.Status == SeatStatus.Active
            select seat.Id).AnyAsync(cancellationToken).ConfigureAwait(false);
        return seated ? Result.Failure(GymErrors.ActiveSeatBlocksDeletion) : Result.Success();
    }

    private async Task AuditAsync(Guid tenantId, Guid actorId, string action, string entityType, Guid entityId, string? detail, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
        await _db.AuditLogs.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, action, entityType, entityId, null, detail, ipAddress, userAgent, utcNow), cancellationToken).ConfigureAwait(false);
}
