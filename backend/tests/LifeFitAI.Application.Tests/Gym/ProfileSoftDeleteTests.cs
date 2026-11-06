using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Chat;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class ProfileSoftDeleteTests
{
    private readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Active_seat_blocks_deletion_and_is_not_released()
    {
        await using var db = CreateDb();
        var seeded = await SeedPersonAsync(db, withSeat: true);
        var result = await Delete(db).DeleteMemberAsync(seeded.TenantId, seeded.ActorId, seeded.MemberId, _now, null, null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("This user currently has an assigned business seat. Revoke the seat before deleting this profile.");
        (await db.MemberProfiles.AnyAsync(item => item.Id == seeded.MemberId)).Should().BeTrue();
        (await db.SubscriptionSeats.SingleAsync()).Status.Should().Be(SeatStatus.Active);
        (await db.Users.SingleAsync(item => item.Id == seeded.UserId)).Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task Member_delete_hides_the_profile_and_keeps_history_and_other_tenants()
    {
        await using var db = CreateDb();
        var seeded = await SeedPersonAsync(db, withSeat: false);
        var result = await Delete(db).DeleteMemberAsync(seeded.TenantId, seeded.ActorId, seeded.MemberId, _now, null, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.MemberProfiles.AnyAsync(item => item.Id == seeded.MemberId)).Should().BeFalse();
        var retained = await db.MemberProfiles.IgnoreQueryFilters().SingleAsync(item => item.Id == seeded.MemberId);
        retained.DeletedAt.Should().Be(_now);
        retained.IsDeleted.Should().BeTrue();
        (await db.GymMemberships.IgnoreQueryFilters().SingleAsync()).Status.Should().Be(GymMembershipStatus.Cancelled);
        (await db.MembershipPayments.CountAsync()).Should().Be(1);
        (await db.Attendance.CountAsync()).Should().Be(1);
        (await db.PtSessions.CountAsync(item => item.Status == PtSessionStatus.Completed)).Should().Be(1);
        (await db.PtSessions.CountAsync(item => item.Status == PtSessionStatus.Cancelled)).Should().Be(1);
        (await db.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == seeded.UserId)).Status.Should().Be(UserStatus.Active);
        (await db.TenantMembers.AnyAsync(item => item.TenantId == seeded.OtherTenantId && item.UserId == seeded.UserId)).Should().BeTrue();
        (await db.TenantMembers.AnyAsync(item => item.TenantId == seeded.TenantId && item.UserId == seeded.UserId)).Should().BeTrue();
        (await db.UserRoles.AnyAsync(item => item.TenantId == seeded.TenantId && item.RoleId == seeded.MemberRoleId)).Should().BeFalse();
        (await db.AuditLogs.AnyAsync(item => item.Action == "DeleteMemberProfile" && item.UserId == seeded.ActorId)).Should().BeTrue();
        (await Delete(db).DeleteMemberAsync(seeded.OtherTenantId, seeded.ActorId, seeded.MemberId, _now, null, null, CancellationToken.None)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Trainer_delete_ends_assignments_without_removing_the_other_business()
    {
        await using var db = CreateDb();
        var seeded = await SeedPersonAsync(db, withSeat: false);
        var result = await Delete(db).DeleteTrainerAsync(seeded.TenantId, seeded.ActorId, seeded.TrainerId, _now, null, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.TrainerProfiles.AnyAsync(item => item.Id == seeded.TrainerId)).Should().BeFalse();
        (await db.TrainerProfiles.AnyAsync(item => item.Id == seeded.OtherTrainerId)).Should().BeTrue();
        (await db.TrainerMemberAssignments.SingleAsync()).Status.Should().Be(TrainerAssignmentStatus.Ended);
        (await db.MemberProfiles.SingleAsync(item => item.Id == seeded.MemberId)).AssignedTrainerId.Should().BeNull();
        (await db.Users.SingleAsync(item => item.Id == seeded.UserId)).DeletedAt.Should().BeNull();
        (await db.TenantMembers.AnyAsync(item => item.TenantId == seeded.OtherTenantId && item.UserId == seeded.UserId)).Should().BeTrue();
    }

    [Fact]
    public async Task Staff_delete_revokes_staff_access_keeps_a_remaining_trainer_and_protects_the_owner()
    {
        await using var db = CreateDb();
        var seeded = await SeedPersonAsync(db, withSeat: false);
        var deleted = await Delete(db).DeleteStaffAsync(seeded.TenantId, seeded.ActorId, seeded.StaffId, _now, null, null, CancellationToken.None);
        var owner = await Delete(db).DeleteStaffAsync(seeded.TenantId, seeded.ActorId, seeded.OwnerStaffId, _now, null, null, CancellationToken.None);

        deleted.IsSuccess.Should().BeTrue();
        (await db.StaffProfiles.AnyAsync(item => item.Id == seeded.StaffId)).Should().BeFalse();
        (await db.TrainerProfiles.AnyAsync(item => item.Id == seeded.TrainerId)).Should().BeTrue();
        (await db.UserRoles.AnyAsync(item => item.UserId == seeded.UserId && item.RoleId == seeded.TrainerRoleId)).Should().BeTrue();
        (await db.UserRoles.AnyAsync(item => item.UserId == seeded.UserId && item.RoleId == seeded.StaffRoleId)).Should().BeFalse();
        (await db.TenantMembers.AnyAsync(item => item.TenantId == seeded.TenantId && item.UserId == seeded.UserId)).Should().BeTrue();
        owner.Error.Code.Should().Be(IdentityErrors.OwnerRemovalRejected.Code);
        (await db.StaffProfiles.AnyAsync(item => item.Id == seeded.OwnerStaffId)).Should().BeTrue();
    }

    [Fact]
    public async Task Parallel_member_deletes_leave_one_soft_deleted_row()
    {
        var database = Guid.NewGuid().ToString();
        await using var setup = CreateDb(database);
        var seeded = await SeedPersonAsync(setup, withSeat: false);
        await using var first = CreateDb(database);
        await using var second = CreateDb(database);
        var results = await Task.WhenAll(
            Delete(first).DeleteMemberAsync(seeded.TenantId, seeded.ActorId, seeded.MemberId, _now, null, null, CancellationToken.None),
            Delete(second).DeleteMemberAsync(seeded.TenantId, seeded.ActorId, seeded.MemberId, _now, null, null, CancellationToken.None));

        results.Should().Contain(item => item.IsSuccess);
        await using var check = CreateDb(database);
        (await check.MemberProfiles.IgnoreQueryFilters().CountAsync(item => item.Id == seeded.MemberId && item.DeletedAt != null)).Should().Be(1);
        (await check.Users.CountAsync(item => item.Id == seeded.UserId && item.DeletedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task Member_only_delete_keeps_chat_history_and_blocks_the_deleted_person()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var tenant = Tenant.Create(tenantId, "North", TenantType.Gym);
        tenant.AssignOwner(actorId);
        db.Tenants.AddRange(tenant, Tenant.Create(otherTenantId, "South", TenantType.Gym));
        db.Users.Add(UserAccount.Create(userId, otherTenantId, "member@gym.test", null, "hash", "salt", "Ada", "Member"));
        db.Users.Add(UserAccount.Create(actorId, tenantId, "owner@gym.test", null, "hash", "salt", "Owen", "Owner"));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), tenantId, userId, _now));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), otherTenantId, userId, _now));
        var roleId = Guid.NewGuid();
        db.Roles.Add(Role.Create(roleId, RoleNames.Member, "Member"));
        db.UserRoles.Add(UserRole.Create(Guid.NewGuid(), userId, roleId, tenantId));
        db.MemberProfiles.Add(MemberProfile.Create(memberId, tenantId, userId, "M-2", DateOnly.FromDateTime(_now), "Ada", "Member"));
        db.ChatConversations.Add(ChatConversation.CreateDirect(conversationId, tenantId, userId, actorId));
        db.ChatParticipants.Add(ChatParticipant.Create(Guid.NewGuid(), tenantId, conversationId, userId, _now));
        db.ChatParticipants.Add(ChatParticipant.Create(Guid.NewGuid(), tenantId, conversationId, actorId, _now));
        db.ChatMessages.Add(ChatMessage.Create(Guid.NewGuid(), tenantId, conversationId, userId, ChatMessageType.Text, "See you at the gym", null));
        await db.SaveChangesAsync();

        var result = await Delete(db).DeleteMemberAsync(tenantId, actorId, memberId, _now, null, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.ChatMessages.CountAsync()).Should().Be(1);
        (await db.ChatParticipants.AnyAsync(item => item.UserId == userId)).Should().BeFalse();
        (await db.ChatParticipants.IgnoreQueryFilters().AnyAsync(item => item.UserId == userId && item.DeletedAt == _now)).Should().BeTrue();
        (await db.ChatParticipants.AnyAsync(item => item.UserId == actorId)).Should().BeTrue();
        (await db.Users.SingleAsync(item => item.Id == userId)).DeletedAt.Should().BeNull();
        (await db.TenantMembers.AnyAsync(item => item.TenantId == otherTenantId && item.UserId == userId)).Should().BeTrue();
        (await db.TenantMembers.AnyAsync(item => item.TenantId == tenantId && item.UserId == userId)).Should().BeFalse();
    }

    private static BusinessProfileSoftDelete Delete(ApplicationDbContext db) =>
        new(db, Substitute.For<IPermissionCacheInvalidator>());

    private async Task<Seed> SeedPersonAsync(ApplicationDbContext db, bool withSeat)
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var otherTrainerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var ownerStaffId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var trainerRoleId = Guid.NewGuid();
        var staffRoleId = Guid.NewGuid();
        var memberRoleId = Guid.NewGuid();
        var tenant = Tenant.Create(tenantId, "North", TenantType.Gym);
        tenant.AssignOwner(actorId);
        var other = Tenant.Create(otherTenantId, "South", TenantType.Gym);
        db.Tenants.AddRange(tenant, other);
        db.Users.Add(UserAccount.Create(userId, otherTenantId, "member@gym.test", null, "hash", "salt", "Ada", "Member"));
        db.Users.Add(UserAccount.Create(actorId, tenantId, "owner@gym.test", null, "hash", "salt", "Owen", "Owner"));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), tenantId, userId, _now));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), otherTenantId, userId, _now));
        db.Roles.Add(Role.Create(trainerRoleId, RoleNames.Trainer, "Trainer"));
        db.Roles.Add(Role.Create(staffRoleId, RoleNames.Receptionist, "Receptionist"));
        db.Roles.Add(Role.Create(memberRoleId, RoleNames.Member, "Member"));
        db.UserRoles.Add(UserRole.Create(Guid.NewGuid(), userId, trainerRoleId, tenantId));
        db.UserRoles.Add(UserRole.Create(Guid.NewGuid(), userId, staffRoleId, tenantId));
        db.UserRoles.Add(UserRole.Create(Guid.NewGuid(), userId, memberRoleId, tenantId));
        var member = MemberProfile.Create(memberId, tenantId, userId, "M-1", DateOnly.FromDateTime(_now), "Ada", "Member");
        member.Update(null, null, null, null, null, null, null, null, ProfileStatus.Active, null, null, trainerId, null);
        db.MemberProfiles.Add(member);
        db.TrainerProfiles.Add(TrainerProfile.Create(trainerId, tenantId, userId, null));
        db.TrainerProfiles.Add(TrainerProfile.Create(otherTrainerId, otherTenantId, userId, null));
        db.StaffProfiles.Add(StaffProfile.Create(staffId, tenantId, userId, "EMP-1", null, StaffType.Receptionist, DateOnly.FromDateTime(_now)));
        db.StaffProfiles.Add(StaffProfile.Create(ownerStaffId, tenantId, actorId, "OWN-1", null, StaffType.Manager, DateOnly.FromDateTime(_now)));
        db.TrainerMemberAssignments.Add(TrainerMemberAssignment.Start(Guid.NewGuid(), tenantId, trainerId, memberId, DateOnly.FromDateTime(_now), actorId));
        var membership = GymMembership.Create(membershipId, tenantId, memberId, null, planId, "GM-1", DateOnly.FromDateTime(_now), DateOnly.FromDateTime(_now).AddDays(30), DateOnly.FromDateTime(_now), false, null);
        db.GymMemberships.Add(membership);
        db.MembershipPayments.Add(MembershipPayment.Create(Guid.NewGuid(), tenantId, memberId, membershipId, 25m, "USD", MembershipPaymentMethod.Cash, DateOnly.FromDateTime(_now), actorId, null, null));
        db.Attendance.Add(AttendanceRecord.CheckIn(Guid.NewGuid(), tenantId, Guid.NewGuid(), memberId, membershipId, CheckInMethod.Manual, _now));
        var package = PtPackage.Create(Guid.NewGuid(), tenantId, "Intro", null, 4, 30, 40m, "USD");
        db.PtPackages.Add(package);
        db.MemberPtAssignments.Add(MemberPtAssignment.Open(Guid.NewGuid(), package, memberId, trainerId, DateOnly.FromDateTime(_now)));
        var completed = PtSession.Schedule(Guid.NewGuid(), tenantId, memberId, trainerId, _now, _now.AddHours(1), "PT", null, null)!;
        completed.Finish(DateOnly.FromDateTime(_now), _now, null, 60);
        db.PtSessions.Add(completed);
        db.PtSessions.Add(PtSession.Schedule(Guid.NewGuid(), tenantId, memberId, trainerId, _now.AddDays(1), _now.AddDays(1).AddHours(1), "PT", null, null)!);
        if (withSeat)
        {
            var subscription = Subscription.ForTenant(Guid.NewGuid(), tenantId, planId, _now.AddDays(-1), 5);
            db.Subscriptions.Add(subscription);
            db.SubscriptionSeats.Add(SubscriptionSeat.Create(Guid.NewGuid(), subscription.Id, userId, actorId, _now));
        }

        await db.SaveChangesAsync();
        return new Seed(tenantId, otherTenantId, actorId, userId, memberId, trainerId, otherTrainerId, staffId, ownerStaffId, trainerRoleId, staffRoleId, memberRoleId);
    }

    private static ApplicationDbContext CreateDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed record Seed(Guid TenantId, Guid OtherTenantId, Guid ActorId, Guid UserId, Guid MemberId, Guid TrainerId, Guid OtherTrainerId, Guid StaffId, Guid OwnerStaffId, Guid TrainerRoleId, Guid StaffRoleId, Guid MemberRoleId);

    private sealed class RowVersionInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries())
            {
                var property = entry.Properties.FirstOrDefault(item => item.Metadata.Name == "RowVersion");
                if (property is not null && property.CurrentValue is null)
                {
                    property.CurrentValue = new byte[] { 1 };
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
