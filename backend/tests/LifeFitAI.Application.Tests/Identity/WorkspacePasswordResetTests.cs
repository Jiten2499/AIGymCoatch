using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.Password;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class WorkspacePasswordResetTests
{
    private readonly Guid _gym = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _owner = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _staff = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Admin_Can_Queue_A_Password_Reset_For_A_Workspace_User()
    {
        var staff = UserAccount.Create(_staff, _gym, "desk@gym.test", null, "hash", "salt", "Front", "Desk");
        var notifier = Substitute.For<IPasswordResetNotifier>();
        var resets = Substitute.For<IPasswordResetRepository>();
        var audit = Substitute.For<IAuditLogRepository>();
        var result = await Handler(staff, member: true, allowed: true, notifier, resets, audit)
            .Handle(new SendWorkspacePasswordResetCommand(staff.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await resets.Received(1).RevokeUnusedAsync(staff.Id, _utc, Arg.Any<CancellationToken>());
        await resets.Received(1).AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
        await notifier.Received(1).NotifyAsync(
            staff.Email,
            Arg.Any<string>(),
            EmailTemplateKeys.PasswordReset,
            _gym,
            Arg.Any<CancellationToken>());
        await audit.Received(1).AddAsync(Arg.Is<AuditLog>(entry => entry.Action == "SendPasswordReset" && entry.EntityId == staff.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_Is_Rejected_Outside_The_Workspace()
    {
        var staff = UserAccount.Create(_staff, _gym, "desk@gym.test", null, "hash", "salt", "Front", "Desk");
        var notifier = Substitute.For<IPasswordResetNotifier>();
        var result = await Handler(staff, member: false, allowed: true, notifier, Substitute.For<IPasswordResetRepository>(), Substitute.For<IAuditLogRepository>())
            .Handle(new SendWorkspacePasswordResetCommand(staff.Id), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.MemberNotFound);
        await notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default!, default!, default, default);
    }

    private SendWorkspacePasswordResetCommandHandler Handler(
        UserAccount target,
        bool member,
        bool allowed,
        IPasswordResetNotifier notifier,
        IPasswordResetRepository resets,
        IAuditLogRepository audit)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_owner);
        actor.TenantId.Returns(_gym);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_owner, _gym, PermissionCodes.UserUpdate, Arg.Any<CancellationToken>()).Returns(allowed);
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        var members = Substitute.For<ITenantMembershipRepository>();
        if (member)
        {
            members.GetActiveAsync(_gym, target.Id, Arg.Any<CancellationToken>()).Returns(TenantMember.Create(Guid.NewGuid(), _gym, target.Id, _utc));
        }

        var tokens = Substitute.For<ISecretTokenFactory>();
        tokens.Create().Returns("raw-reset-token");
        tokens.Hash("raw-reset-token").Returns("hashed-reset-token");
        var settings = Substitute.For<ITokenSettings>();
        settings.ResetLifetime.Returns(TimeSpan.FromMinutes(30));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        return new SendWorkspacePasswordResetCommandHandler(
            actor,
            permissions,
            members,
            users,
            resets,
            tokens,
            notifier,
            settings,
            audit,
            unitOfWork,
            clock,
            Substitute.For<IClientContext>());
    }
}
