using System.Security.Claims;
using FluentAssertions;
using LifeFitAI.Api.Middleware;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MemberFoundationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _actor = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");

    [Fact]
    public async Task Search_IsTenantScoped_FiltersStatus_AndPaginates()
    {
        await using var db = CreateDb();
        var ada = UserAccount.Create(Guid.NewGuid(), _tenantA, "ada@gym.test", "+15550001111", "hash", "salt", "Ada", "Lovelace");
        var grace = UserAccount.Create(Guid.NewGuid(), _tenantA, "grace@gym.test", "+15550002222", "hash", "salt", "Grace", "Hopper");
        var other = UserAccount.Create(Guid.NewGuid(), _tenantB, "other@gym.test", "+15550003333", "hash", "salt", "Other", "Gym");
        db.Users.AddRange(ada, grace, other);
        foreach (var user in new[] { ada, grace, other })
        {
            db.Entry(user).Property(account => account.RowVersion).CurrentValue = new byte[] { 1 };
        }
        var inactive = MemberProfile.Create(Guid.NewGuid(), _tenantA, grace.Id, "M-GRACE", new DateOnly(2026, 9, 1));
        inactive.ChangeStatus(ProfileStatus.Inactive);
        var adaProfile = MemberProfile.Create(Guid.NewGuid(), _tenantA, ada.Id, "M-ADA", new DateOnly(2026, 10, 1));
        var otherProfile = MemberProfile.Create(Guid.NewGuid(), _tenantB, other.Id, "M-OTHER", new DateOnly(2026, 10, 1));
        db.MemberProfiles.AddRange(adaProfile, inactive, otherProfile);
        foreach (var profile in new[] { adaProfile, inactive, otherProfile })
        {
            db.Entry(profile).Property(item => item.RowVersion).CurrentValue = new byte[] { 1 };
        }
        await db.SaveChangesAsync();
        var people = new PeopleRepository(db);

        var named = await people.SearchMembersAsync(_tenantA, "Ada Love", null, null, null, new PageQuery(1, 20, "name"), CancellationToken.None);
        var mobile = await people.SearchMembersAsync(_tenantA, "0002222", null, null, null, new PageQuery(1, 20, null), CancellationToken.None);
        var active = await people.SearchMembersAsync(_tenantA, null, ProfileStatus.Active, null, null, new PageQuery(1, 20, null), CancellationToken.None);
        var page = await people.SearchMembersAsync(_tenantA, null, null, null, null, new PageQuery(1, 1, "name"), CancellationToken.None);
        var empty = await people.SearchMembersAsync(Guid.NewGuid(), null, null, null, null, new PageQuery(1, 20, null), CancellationToken.None);

        named.TotalCount.Should().Be(1);
        named.Items[0].FullName.Should().Be("Ada Lovelace");
        mobile.Items.Should().ContainSingle(item => item.FullName == "Grace Hopper");
        active.Items.Should().ContainSingle(item => item.FullName == "Ada Lovelace");
        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle();
        empty.Items.Should().BeEmpty();
        named.Items.Should().NotContain(item => item.FullName == "Other Gym");
    }

    [Fact]
    public void Profile_KeepsTheExistingUserRelationship()
    {
        var userId = Guid.NewGuid();
        var profile = MemberProfile.Create(Guid.NewGuid(), _tenantA, userId, "M-1", new DateOnly(2026, 10, 1));

        profile.UserId.Should().Be(userId);
        profile.TenantId.Should().Be(_tenantA);
    }

    [Fact]
    public void PhotoRules_AcceptImagesAndRejectDisguisedFiles()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var text = "MZ executable"u8.ToArray();
        MemberFieldRules.TryContentType(jpeg, "image/jpeg", out var type).Should().BeTrue();
        type.Should().Be("image/jpeg");
        MemberFieldRules.TryContentType(jpeg, "image/png", out _).Should().BeFalse();
        MemberFieldRules.TryContentType(text, "image/jpeg", out _).Should().BeFalse();
        new CreateMemberCommandValidator().Validate(new CreateMemberCommand("not-an-email", null, "", "Lee", "12", null, null, null, false)).IsValid.Should().BeFalse();
        new CreateMemberCommandValidator().Validate(new CreateMemberCommand("", null, "Rahul", "Shah", "9876543210", null, null, null, false, JoinDate: new DateOnly(2026, 10, 1))).IsValid.Should().BeTrue();
        var walkIn = MemberProfile.Create(Guid.NewGuid(), Guid.NewGuid(), null, "M-WALK", new DateOnly(2026, 10, 1), "Rahul", "Shah", "9876543210");
        walkIn.UserId.Should().BeNull();
        walkIn.FirstName.Should().Be("Rahul");
    }

    [Fact]
    public async Task PermissionAndFeatureDenials_DoNotQueryMembers()
    {
        var people = Substitute.For<IPeopleRepository>();
        var denied = Guard(IdentityErrors.Forbidden);
        var disabled = Guard(GymErrors.FeatureDisabled);
        var read = new GetMemberQueryHandler(denied, Actor(), people, Substitute.For<IMemberAccessScope>());
        var photo = new GetMemberPhotoQueryHandler(disabled, Actor(), people, Substitute.For<IFileStorage>());

        (await read.Handle(new GetMemberQuery(Guid.NewGuid()), CancellationToken.None)).IsFailure.Should().BeTrue();
        (await photo.Handle(new GetMemberPhotoQuery(Guid.NewGuid()), CancellationToken.None)).Error.Should().Be(GymErrors.FeatureDisabled);
        await people.DidNotReceive().GetMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await people.DidNotReceive().ReadMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PhotoAndTrainer_StayInsideTheActingTenant()
    {
        var people = Substitute.For<IPeopleRepository>();
        var storage = Substitute.For<IFileStorage>();
        people.GetMemberAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        people.GetTrainerAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TrainerProfile?)null);
        var photo = new GetMemberPhotoQueryHandler(Guard(null), Actor(), people, storage);
        var memberUserId = Guid.NewGuid();
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(memberUserId, Arg.Any<CancellationToken>()).Returns(UserAccount.Create(memberUserId, _tenantA, "ada@gym.test", null, "hash", "salt", "Ada", "Lovelace"));
        var update = new UpdateMemberCommandHandler(Guard(null), Actor(), people, users, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

        (await photo.Handle(new GetMemberPhotoQuery(Guid.NewGuid()), CancellationToken.None)).Error.Code.Should().Be(GymErrors.NotFound.Code);
        var trainerId = Guid.NewGuid();
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, memberUserId, "M-1", new DateOnly(2026, 10, 1));
        people.GetMemberAsync(_tenantA, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var updated = await update.Handle(new UpdateMemberCommand(member.Id, "Ada", "Lovelace", null, null, null, null, null, null, null, null, null, null, null, null, trainerId, ProfileStatus.Active), CancellationToken.None);

        updated.IsFailure.Should().BeTrue();
        await storage.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await people.Received().GetTrainerAsync(_tenantA, trainerId, Arg.Any<CancellationToken>());
        await people.DidNotReceive().GetTrainerAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidPhoto_IsRejectedBeforeStorage()
    {
        var people = Substitute.For<IPeopleRepository>();
        var storage = Substitute.For<IFileStorage>();
        var profile = MemberProfile.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), "M-1", new DateOnly(2026, 10, 1));
        people.GetMemberAsync(_tenantA, profile.Id, Arg.Any<CancellationToken>()).Returns(profile);
        var handler = new UploadMemberPhotoCommandHandler(Guard(null), Actor(), people, storage, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

        var result = await handler.Handle(new UploadMemberPhotoCommand(profile.Id, "image/jpeg", "not-an-image"u8.ToArray()), CancellationToken.None);

        result.Error.Should().Be(GymErrors.InvalidPhoto);
        await storage.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendedWorkspace_StopsTheMemberRoute()
    {
        var tenant = Tenant.Create(_tenantA, "North Gym", TenantType.Gym);
        tenant.Suspend();
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetAsync(_tenantA, Arg.Any<CancellationToken>()).Returns(tenant);
        var called = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/members";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", _tenantA.ToString())], "test"));

        await middleware.InvokeAsync(context, tenants, new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())));

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task LocalStorage_RoundTripsOnlyInsideItsRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "lifefit-members-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalFileStorage(new StorageOptions { RootPath = root });
        var key = MemberFieldRules.PhotoKey(_tenantA, Guid.NewGuid(), Guid.NewGuid());
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        await storage.SaveAsync(key, bytes, CancellationToken.None);
        (await storage.ReadAsync(key, CancellationToken.None)).Should().Equal(bytes);
        (await storage.ReadAsync("../secret", CancellationToken.None)).Should().BeNull();
        await storage.DeleteAsync(key, CancellationToken.None);
        Directory.Delete(root, true);
    }

    private IOperationGuard Guard(Error? error)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(error is null ? Result.Success() : Result.Failure(error));
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_actor);
        actor.TenantId.Returns(_tenantA);
        return actor;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));
        return clock;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
