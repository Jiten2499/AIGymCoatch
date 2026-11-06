using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Staffing;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class CoachTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    [Fact]
    public void AddCoach_RejectsABlankEmail()
    {
        var result = new AddCoachCommandValidator().Validate(new AddCoachCommand(" ", "Ada", "Lovelace", null, null, null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AddCoach_RejectsWhenStaffManagementIsDisabled()
    {
        var guard = Substitute.For<IOperationGuard>();
        var accounts = Substitute.For<IAccountLinker>();
        guard.AllowAsync(PermissionCodes.StaffCreate, FeatureCodes.StaffManagement, Arg.Any<CancellationToken>()).Returns(Result.Failure(GymErrors.FeatureDisabled));
        var handler = Handler(guard, accounts, Substitute.For<IPeopleRepository>(), Substitute.For<IUserAccountRepository>());

        var result = await handler.Handle(new AddCoachCommand("ada@gym.test", "Ada", "Lovelace", null, "Strength", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gym.feature_disabled");
        await accounts.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCoach_RejectsAnExistingTrainerAndCreatesANewAccountPassword()
    {
        var user = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "ada@gym.test", null, "hash", "salt", "Ada", "Lovelace");
        var people = Substitute.For<IPeopleRepository>();
        var users = Substitute.For<IUserAccountRepository>();
        var accounts = Substitute.For<IAccountLinker>();
        users.GetByEmailAsync("ada@gym.test", Arg.Any<CancellationToken>()).Returns(user);
        accounts.ResolveAsync("ada@gym.test", null, "Ada", "Lovelace", null, Arg.Any<CancellationToken>()).Returns(Result.Success(user));
        people.GetTrainerByUserAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(TrainerProfile.Create(Guid.NewGuid(), _tenantId, user.Id, null));
        var conflict = await Handler(Allow(), accounts, people, users).Handle(new AddCoachCommand("ada@gym.test", "Ada", "Lovelace", null, null, null), CancellationToken.None);
        conflict.Error.Code.Should().Be("gym.conflict");
        await people.DidNotReceive().AddTrainerAsync(Arg.Any<TrainerProfile>(), Arg.Any<CancellationToken>());

        var created = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "grace@gym.test", null, "hash", "salt", "Grace", "Hopper");
        var freshPeople = Substitute.For<IPeopleRepository>();
        var freshUsers = Substitute.For<IUserAccountRepository>();
        var freshAccounts = Substitute.For<IAccountLinker>();
        var roles = Substitute.For<IRoleRepository>();
        freshUsers.GetByEmailAsync("grace@gym.test", Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        freshAccounts.ResolveAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success(created));
        roles.GetByNameAsync(RoleNames.Trainer, Arg.Any<CancellationToken>()).Returns(Role.Create(Guid.NewGuid(), RoleNames.Trainer, null));
        roles.GetRoleNamesAsync(created.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(new List<string>());
        var createdResult = await Handler(Allow(), freshAccounts, freshPeople, freshUsers, roles).Handle(new AddCoachCommand("grace@gym.test", "Grace", "Hopper", null, "Strength", null), CancellationToken.None);

        createdResult.IsSuccess.Should().BeTrue();
        createdResult.Value.TemporaryPassword.Should().BeNull();
        createdResult.Value.Email.Should().Be("grace@gym.test");
        createdResult.Value.Specialization.Should().Be("Strength");
        await freshPeople.Received(1).AddTrainerAsync(Arg.Any<TrainerProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTrainer_RenamesTheCoachInsideTheCurrentTenant()
    {
        var user = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "ada@gym.test", null, "hash", "salt", "Ada", "Lovelace");
        var trainer = TrainerProfile.Create(Guid.NewGuid(), _tenantId, user.Id, null);
        var people = Substitute.For<IPeopleRepository>();
        var users = Substitute.For<IUserAccountRepository>();
        people.GetTrainerAsync(_tenantId, trainer.Id, Arg.Any<CancellationToken>()).Returns(trainer);
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var handler = new UpdateTrainerCommandHandler(Allow(), Actor(), people, users, Substitute.For<IUnitOfWork>());

        var result = await handler.Handle(new UpdateTrainerCommand(trainer.Id, "Leads strength", "Strength", 8, "ACE", null, null, ProfileStatus.Active, "Grace", "Hopper"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Grace");
        result.Value.LastName.Should().Be("Hopper");
        result.Value.Specialization.Should().Be("Strength");
        await people.Received(1).GetTrainerAsync(_tenantId, trainer.Id, Arg.Any<CancellationToken>());
    }

    private AddCoachCommandHandler Handler(IOperationGuard guard, IAccountLinker accounts, IPeopleRepository people, IUserAccountRepository users, IRoleRepository? roles = null)
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        var memberships = Substitute.For<ITenantMembershipRepository>();
        return new AddCoachCommandHandler(
            guard,
            Actor(),
            accounts,
            users,
            memberships,
            roles ?? Substitute.For<IRoleRepository>(),
            people,
            Substitute.For<IBranchRepository>(),
            Substitute.For<IAuditLogRepository>(),
            unit,
            Clock(),
            Substitute.For<IClientContext>(),
            Substitute.For<ITenantRepository>());
    }

    private IOperationGuard Allow()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_actorId);
        actor.TenantId.Returns(_tenantId);
        return actor;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));
        return clock;
    }
}
