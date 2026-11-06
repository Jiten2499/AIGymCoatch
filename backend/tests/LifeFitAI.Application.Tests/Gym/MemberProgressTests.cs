using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MemberProgressTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _actor = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateOnly _today = new(2026, 10, 24);

    [Fact]
    public async Task Save_AppendsHistory_AndLeavesMissingMeasurementsNull()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, userId: null);

        var first = await harness.Handler.Handle(Command(harness.MemberId, null, 70.5m, null, _today.AddDays(-7)), CancellationToken.None);
        var second = await harness.Handler.Handle(Command(harness.MemberId, null, 71m, 80m, _today), CancellationToken.None);
        var history = await harness.Handler.Handle(new ListMemberProgressQuery(harness.MemberId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        (await db.MemberProgressAssessments.CountAsync()).Should().Be(2);
        (await db.MemberProgressAssessments.SingleAsync(item => item.Id == first.Value)).Weight.Should().Be(70.5m);
        history.Value[0].Weight.Change.Should().Be(0.5m);
        history.Value[1].Waist.Value.Should().BeNull();
        history.Value[1].Waist.Change.Should().BeNull();
    }

    [Fact]
    public async Task Edit_UpdatesOnlyTheSelectedAssessment()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(UserAccount.Create(userId, _tenant, "member@example.com", null, "hash", "salt", "Priya", "Sharma"));
        var harness = await SeedAsync(db, userId);

        var first = await harness.Handler.Handle(Command(harness.MemberId, null, 70m, null), CancellationToken.None);
        var second = await harness.Handler.Handle(Command(harness.MemberId, null, 72m, null), CancellationToken.None);
        var edited = await harness.Handler.Handle(Command(harness.MemberId, first.Value, 69m, 70m), CancellationToken.None);

        edited.IsSuccess.Should().BeTrue();
        (await db.MemberProgressAssessments.CountAsync()).Should().Be(2);
        (await db.BodyMeasurements.CountAsync()).Should().Be(2);
        (await db.MemberProgressAssessments.SingleAsync(item => item.Id == first.Value)).Weight.Should().Be(69m);
        (await db.MemberProgressAssessments.SingleAsync(item => item.Id == second.Value)).Weight.Should().Be(72m);
    }

    [Fact]
    public async Task Validation_RejectsZeroAndAnotherTrainerCannotEdit()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, userId: null);
        var saved = await harness.Handler.Handle(Command(harness.MemberId, null, 70m, null), CancellationToken.None);
        var zero = await harness.Handler.Handle(Command(harness.MemberId, null, 0m, null), CancellationToken.None);
        var otherUserId = Guid.NewGuid();
        var otherTrainer = TrainerProfile.Create(Guid.NewGuid(), _tenant, otherUserId, null);
        db.TrainerProfiles.Add(otherTrainer);
        db.TrainerMemberAssignments.Add(TrainerMemberAssignment.Start(Guid.NewGuid(), _tenant, otherTrainer.Id, harness.MemberId, _today, _actor));
        await db.SaveChangesAsync();
        harness.Actor.UserId.Returns(otherUserId);

        var edited = await harness.Handler.Handle(Command(harness.MemberId, saved.Value, 68m, null), CancellationToken.None);

        zero.IsSuccess.Should().BeFalse();
        edited.Error.Code.Should().Be(IdentityErrors.Forbidden.Code);
        (await db.MemberProgressAssessments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Photo_RequiresConsent_AndStaysInsideTheAssignedTenant()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, userId: null);
        var saved = await harness.Handler.Handle(Command(harness.MemberId, null, 70m, null), CancellationToken.None);
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var denied = await harness.Handler.Handle(new UploadMemberProgressPhotoCommand(harness.MemberId, saved.Value, "image/jpeg", jpeg), CancellationToken.None);
        await harness.Handler.Handle(new SaveMemberProgressCommand(harness.MemberId, saved.Value, _today, 70m, null, null, null, null, null, null, null, null, null, null, null, true), CancellationToken.None);
        var stranger = MemberProfile.Create(Guid.NewGuid(), _tenant, null, "M-2", _today, "Neha", "Gupta", null);
        db.MemberProfiles.Add(stranger);
        await db.SaveChangesAsync();

        var uploaded = await harness.Handler.Handle(new UploadMemberProgressPhotoCommand(harness.MemberId, saved.Value, "image/jpeg", jpeg), CancellationToken.None);
        var foreign = await harness.Handler.Handle(new UploadMemberProgressPhotoCommand(stranger.Id, saved.Value, "image/jpeg", jpeg), CancellationToken.None);
        harness.Actor.TenantId.Returns(_other);
        var otherTenant = await harness.Handler.Handle(new ListMemberProgressQuery(harness.MemberId), CancellationToken.None);

        denied.IsSuccess.Should().BeFalse();
        uploaded.IsSuccess.Should().BeTrue();
        foreign.Error.Code.Should().Be(GymErrors.NotFound.Code);
        otherTenant.IsSuccess.Should().BeFalse();
        await harness.Storage.Received(1).SaveAsync(Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    private SaveMemberProgressCommand Command(Guid memberId, Guid? assessmentId, decimal? weight, decimal? waist, DateOnly? date = null) =>
        new(memberId, assessmentId, date ?? _today, weight, waist, null, null, null, null, null, null, null, null, null, null, false);

    private async Task<Harness> SeedAsync(ApplicationDbContext db, Guid? userId)
    {
        if (!await db.Tenants.AnyAsync())
        {
            db.Tenants.Add(Tenant.Create(_tenant, "A", TenantType.Gym));
            db.Tenants.Add(Tenant.Create(_other, "B", TenantType.Gym));
            db.Users.Add(UserAccount.Create(_actor, _tenant, "coach@example.com", null, "hash", "salt", "Rahul", "Kumar"));
            db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, _actor, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var trainer = await new TrainerWorkspace(db).CreateFromUserAsync(_tenant, _actor, "T-1", "Strength", null, _today, true, CancellationToken.None);
        var member = MemberProfile.Create(Guid.NewGuid(), _tenant, userId, "M-1", _today, "Priya", "Sharma", null);
        member.Update(null, null, null, null, null, null, null, null, ProfileStatus.Active, assignedTrainerId: trainer!.Id);
        db.MemberProfiles.Add(member);
        await db.SaveChangesAsync();
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenant);
        actor.UserId.Returns(_actor);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc));
        var storage = Substitute.For<IFileStorage>();
        storage.IsSafeKey(Arg.Any<string>()).Returns(true);
        var audit = Substitute.For<IAuditLogRepository>();
        var client = Substitute.For<IClientContext>();
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var archive = new ProgressPhotoArchive(features, Substitute.For<ITenantStorageConnectionRepository>(), Substitute.For<IFileStorageService>(), storage);
        var handler = new TrainerProgressHandlers(actor, new TrainerMobileStore(db), storage, archive, audit, new Unit(db), clock, client);
        return new Harness(actor, handler, storage, member.Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed record Harness(ICurrentActor Actor, TrainerProgressHandlers Handler, IFileStorage Storage, Guid MemberId);

    private sealed class Unit : IUnitOfWork
    {
        private readonly ApplicationDbContext _db;
        public Unit(ApplicationDbContext db) => _db = db;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) => action(cancellationToken);
    }

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
