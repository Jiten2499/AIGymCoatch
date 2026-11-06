using FluentAssertions;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.Profile;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class ProfilePhotoTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lifefit-profile-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb2");
    private readonly Guid _userA = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly Guid _userB = Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd2");
    private readonly DateTime _utc = new(2026, 10, 25, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Upload_Replace_AndRemove_UsePrivateStorageForTheSignedInUser()
    {
        var ada = UserAccount.Create(_userA, _tenantA, "ada@lifefit.test", null, "hash", "salt", "Ada", "Lovelace");
        var other = UserAccount.Create(_userB, _tenantB, "grace@lifefit.test", null, "hash", "salt", "Grace", "Hopper");
        var storage = new LocalFileStorage(new StorageOptions { RootPath = _root });
        var handler = Handler(ada, other, storage, _userA, _tenantA, shareTenant: false);

        var saved = await handler.Handle(new UploadMyProfilePhotoCommand("image/jpeg", Jpeg()), CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();
        saved.Value.HasPhoto.Should().BeTrue();
        var stored = await storage.ReadAsync(ada.PhotoStorageKey!, CancellationToken.None);
        stored.Should().Equal(Jpeg());

        var replaced = await handler.Handle(new UploadMyProfilePhotoCommand("image/jpeg", Jpeg(9)), CancellationToken.None);
        replaced.Value.HasPhoto.Should().BeTrue();
        ada.PhotoStorageKey.Should().NotBeNull();
        (await storage.ReadAsync(ada.PhotoStorageKey!, CancellationToken.None)).Should().Equal(Jpeg(9));
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Should().HaveCount(1);

        var rejected = await handler.Handle(new UploadMyProfilePhotoCommand("image/gif", new byte[32]), CancellationToken.None);
        rejected.Error.Should().Be(IdentityErrors.InvalidPhoto);
        (await storage.ReadAsync(ada.PhotoStorageKey!, CancellationToken.None)).Should().Equal(Jpeg(9));

        var stranger = Handler(ada, other, storage, _userB, _tenantB, shareTenant: false);
        (await stranger.Handle(new GetProfilePhotoQuery(_userA), CancellationToken.None)).Error.Should().Be(IdentityErrors.Forbidden);
        other.PhotoStorageKey.Should().BeNull();

        var removed = await handler.Handle(new RemoveMyProfilePhotoCommand(), CancellationToken.None);
        removed.Value.HasPhoto.Should().BeFalse();
        ada.PhotoStorageKey.Should().BeNull();
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task SameWorkspace_CanReadAnotherUsersPhoto()
    {
        var ada = UserAccount.Create(_userA, _tenantA, "ada@lifefit.test", null, "hash", "salt", "Ada", "Lovelace");
        var grace = UserAccount.Create(_userB, _tenantA, "grace@lifefit.test", null, "hash", "salt", "Grace", "Hopper");
        var storage = new LocalFileStorage(new StorageOptions { RootPath = _root });
        var owner = Handler(ada, grace, storage, _userA, _tenantA, shareTenant: true);
        (await owner.Handle(new UploadMyProfilePhotoCommand(null, Jpeg()), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var reader = Handler(ada, grace, storage, _userB, _tenantA, shareTenant: true);
        var photo = await reader.Handle(new GetProfilePhotoQuery(_userA), CancellationToken.None);
        photo.IsSuccess.Should().BeTrue();
        photo.Value.Content.Should().Equal(Jpeg());
        photo.Value.ContentType.Should().Be("image/jpeg");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private ProfilePhotoHandler Handler(UserAccount ada, UserAccount other, IFileStorage storage, Guid actorId, Guid tenantId, bool shareTenant)
    {
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(ada.Id, Arg.Any<CancellationToken>()).Returns(ada);
        users.GetByIdAsync(other.Id, Arg.Any<CancellationToken>()).Returns(other);
        var members = Substitute.For<ITenantMembershipRepository>();
        if (shareTenant)
        {
            members.GetActiveAsync(tenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TenantMember.Create(Guid.NewGuid(), tenantId, ada.Id, _utc));
        }

        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(actorId);
        actor.TenantId.Returns(tenantId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return new ProfilePhotoHandler(actor, users, members, storage, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), clock, Substitute.For<IClientContext>());
    }

    private static byte[] Jpeg(byte marker = 1)
    {
        var bytes = new byte[16];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = marker;
        return bytes;
    }
}
