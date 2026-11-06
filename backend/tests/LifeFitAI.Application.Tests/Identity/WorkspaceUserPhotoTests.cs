using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.Profile;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class WorkspaceUserPhotoTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lifefit-staff-photo-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _gym = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _owner = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _staff = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly Guid _outsider = Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd1");
    private readonly DateTime _utc = new(2026, 10, 28, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Owner_SavesAPhotoForAWorkspaceMember()
    {
        var staff = UserAccount.Create(_staff, _gym, "desk@gym.test", null, "hash", "salt", "Front", "Desk");
        var storage = new LocalFileStorage(new StorageOptions { RootPath = _root });
        var saved = await Handler(staff, member: true, allowed: true, storage).Handle(new UploadWorkspaceUserPhotoCommand(staff.Id, "image/jpeg", Jpeg()), CancellationToken.None);

        saved.IsSuccess.Should().BeTrue();
        staff.PhotoContentType.Should().Be("image/jpeg");
        (await storage.ReadAsync(staff.PhotoStorageKey!, CancellationToken.None)).Should().Equal(Jpeg());
    }

    [Fact]
    public async Task Photo_IsRejectedForSomeoneOutsideTheWorkspace()
    {
        var outsider = UserAccount.Create(_outsider, Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1"), "other@gym.test", null, "hash", "salt", "Other", "Person");
        var storage = new LocalFileStorage(new StorageOptions { RootPath = _root });
        var saved = await Handler(outsider, member: false, allowed: true, storage).Handle(new UploadWorkspaceUserPhotoCommand(outsider.Id, "image/png", Jpeg()), CancellationToken.None);

        saved.Error.Should().Be(IdentityErrors.Forbidden);
        outsider.PhotoStorageKey.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private UploadWorkspaceUserPhotoHandler Handler(UserAccount target, bool member, bool allowed, IFileStorage storage)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_owner);
        actor.TenantId.Returns(_gym);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_owner, _gym, PermissionCodes.UserCreate, Arg.Any<CancellationToken>()).Returns(allowed);
        var tenants = Substitute.For<ITenantRepository>();
        var gym = Tenant.Create(_gym, "North Gym", TenantType.Gym);
        tenants.GetAsync(_gym, Arg.Any<CancellationToken>()).Returns(gym);
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        var members = Substitute.For<ITenantMembershipRepository>();
        if (member)
        {
            members.GetActiveAsync(_gym, target.Id, Arg.Any<CancellationToken>()).Returns(TenantMember.Create(Guid.NewGuid(), _gym, target.Id, _utc));
        }

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return new UploadWorkspaceUserPhotoHandler(actor, permissions, tenants, users, members, storage, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), clock, Substitute.For<IClientContext>());
    }

    private static byte[] Jpeg()
    {
        var bytes = new byte[16];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 1;
        return bytes;
    }
}
