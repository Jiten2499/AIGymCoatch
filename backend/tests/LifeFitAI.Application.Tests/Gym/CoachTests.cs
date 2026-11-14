using FluentAssertions;
using LifeFitAI.Application.Email;
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
        var result = new AddCoachCommandValidator().Validate(new AddCoachCommand(" ", "Ada", "Lovelace", "+9779800000000", null, null, false, Designation: "Personal Trainer", JoiningDate: new DateOnly(2026, 11, 12)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddCoach_RequiresPhoneDesignationAndJoiningDate()
    {
        var result = new AddCoachCommandValidator().Validate(new AddCoachCommand("ada@gym.test", "Ada", "Lovelace", null, null, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddCoachCommand.Phone));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddCoachCommand.Designation));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddCoachCommand.JoiningDate));
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
        accounts.ResolveAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success(user));
        people.GetTrainerByUserAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(TrainerProfile.Create(Guid.NewGuid(), _tenantId, user.Id, null));
        var conflict = await Handler(Allow(), accounts, people, users).Handle(new AddCoachCommand("ada@gym.test", "Ada", "Lovelace", null, null, null), CancellationToken.None);
        conflict.Error.Code.Should().Be("gym.conflict");
        await people.DidNotReceive().AddTrainerAsync(Arg.Any<TrainerProfile>(), Arg.Any<CancellationToken>());

        var created = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "grace@gym.test", null, "hash", "salt", "Grace", "Hopper");
        var freshPeople = Substitute.For<IPeopleRepository>();
        var freshUsers = Substitute.For<IUserAccountRepository>();
        var freshAccounts = Substitute.For<IAccountLinker>();
        var roles = Substitute.For<IRoleRepository>();
        string? issued = null;
        freshUsers.GetByEmailAsync("grace@gym.test", Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        freshAccounts.ResolveAsync(Arg.Any<string>(), Arg.Do<string?>(value => issued = value), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success(created));
        roles.GetByNameAsync(RoleNames.Trainer, Arg.Any<CancellationToken>()).Returns(Role.Create(Guid.NewGuid(), RoleNames.Trainer, null));
        roles.GetRoleNamesAsync(created.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(new List<string>());
        var createdResult = await Handler(Allow(), freshAccounts, freshPeople, freshUsers, roles).Handle(new AddCoachCommand("grace@gym.test", "Grace", "Hopper", null, "Strength", null), CancellationToken.None);

        createdResult.IsSuccess.Should().BeTrue();
        createdResult.Value.TemporaryPassword.Should().BeNull();
        issued.Should().NotBeNullOrWhiteSpace();
        createdResult.Value.Email.Should().Be("grace@gym.test");
        createdResult.Value.Specialization.Should().Be("Strength");
        createdResult.Value.StaffProfileId.Should().NotBeNull();
        await freshPeople.Received(1).AddTrainerAsync(Arg.Any<TrainerProfile>(), Arg.Any<CancellationToken>());
        await freshPeople.Received(1).AddStaffAsync(Arg.Any<StaffProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCoach_SendsAnActivationLinkWhenTheEmailHasNoBusinessWorkspace()
    {
        var user = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "ada@gym.test", null, "hash", "salt", "Ada", "Lovelace");
        var people = Substitute.For<IPeopleRepository>();
        var users = Substitute.For<IUserAccountRepository>();
        var accounts = Substitute.For<IAccountLinker>();
        var roles = Substitute.For<IRoleRepository>();
        var memberships = Substitute.For<ITenantMembershipRepository>();
        var activation = Substitute.For<IAccountActivationMailer>();
        users.GetByEmailAsync("ada@gym.test", Arg.Any<CancellationToken>()).Returns(user);
        memberships.ListForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(new List<WorkspaceSummary> { new(Guid.NewGuid(), "LifeFit", TenantType.Platform, TenantStatus.Active, []) });
        accounts.ResolveAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success(user));
        roles.GetByNameAsync(RoleNames.Trainer, Arg.Any<CancellationToken>()).Returns(Role.Create(Guid.NewGuid(), RoleNames.Trainer, null));
        roles.GetRoleNamesAsync(user.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(new List<string>());

        var result = await Handler(Allow(), accounts, people, users, roles, memberships, activation).Handle(new AddCoachCommand("ada@gym.test", "Ada", "Lovelace", null, "Strength", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TemporaryPassword.Should().BeNull();
        await activation.Received(1).SendAsync(Arg.Is<AccountActivationNotice>(notice => notice.Email == "ada@gym.test" && !notice.ExistingAccount), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCoach_KeepsThePasswordWhenTheEmailIsActiveInAnotherBusiness()
    {
        var user = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "ada@gym.test", null, "hash", "salt", "Ada", "Lovelace");
        var people = Substitute.For<IPeopleRepository>();
        var users = Substitute.For<IUserAccountRepository>();
        var accounts = Substitute.For<IAccountLinker>();
        var roles = Substitute.For<IRoleRepository>();
        var memberships = Substitute.For<ITenantMembershipRepository>();
        users.GetByEmailAsync("ada@gym.test", Arg.Any<CancellationToken>()).Returns(user);
        memberships.ListForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(new List<WorkspaceSummary> { new(Guid.NewGuid(), "Other Gym", TenantType.Gym, TenantStatus.Active, []) });
        accounts.ResolveAsync("ada@gym.test", null, "Ada", "Lovelace", null, Arg.Any<CancellationToken>()).Returns(Result.Success(user));
        roles.GetByNameAsync(RoleNames.Trainer, Arg.Any<CancellationToken>()).Returns(Role.Create(Guid.NewGuid(), RoleNames.Trainer, null));
        roles.GetRoleNamesAsync(user.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(new List<string>());

        var result = await Handler(Allow(), accounts, people, users, roles, memberships).Handle(new AddCoachCommand("ada@gym.test", "Ada", "Lovelace", null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TemporaryPassword.Should().BeNull();
        await accounts.Received(1).ResolveAsync("ada@gym.test", null, "Ada", "Lovelace", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCoach_PersistsExpandedProfileFieldsForFreelanceCoach()
    {
        var created = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "coach@gym.test", null, "hash", "salt", "Rahul", "Sharma");
        var people = Substitute.For<IPeopleRepository>();
        var users = Substitute.For<IUserAccountRepository>();
        var accounts = Substitute.For<IAccountLinker>();
        var roles = Substitute.For<IRoleRepository>();
        users.GetByEmailAsync("coach@gym.test", Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        accounts.ResolveAsync("coach@gym.test", Arg.Any<string?>(), "Rahul", "Sharma", "+9779800112233", Arg.Any<CancellationToken>()).Returns(Result.Success(created));
        roles.GetByNameAsync(RoleNames.Trainer, Arg.Any<CancellationToken>()).Returns(Role.Create(Guid.NewGuid(), RoleNames.Trainer, null));
        roles.GetRoleNamesAsync(created.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(new List<string>());
        StaffProfile? savedStaff = null;
        TrainerProfile? savedTrainer = null;
        people.When(repo => repo.AddStaffAsync(Arg.Any<StaffProfile>(), Arg.Any<CancellationToken>())).Do(call => savedStaff = call.Arg<StaffProfile>());
        people.When(repo => repo.AddTrainerAsync(Arg.Any<TrainerProfile>(), Arg.Any<CancellationToken>())).Do(call => savedTrainer = call.Arg<TrainerProfile>());

        var result = await Handler(Allow(), accounts, people, users, roles).Handle(
            new AddCoachCommand(
                "coach@gym.test",
                "Rahul",
                "Sharma",
                "+9779800112233",
                "Strength Training, Weight Loss",
                null,
                false,
                new DateOnly(1995, 4, 12),
                "Male",
                "Kathmandu",
                "Sita Sharma",
                "+9779800998877",
                "Senior Trainer",
                "Leads strength blocks",
                "ACE CPT · Issued by ACE",
                5,
                EmploymentType.Freelance,
                new DateOnly(2026, 11, 1),
                null,
                "Previous workplace: Peak Gym",
                ProfileStatus.Active,
                true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StaffProfileId.Should().Be(savedStaff!.Id);
        savedStaff.Designation.Should().Be("Senior Trainer");
        savedStaff.EmploymentType.Should().Be(EmploymentType.Freelance);
        savedStaff.EmergencyContactName.Should().Be("Sita Sharma");
        savedTrainer!.Specialization.Should().Be("Strength Training, Weight Loss");
        savedTrainer.ExperienceYears.Should().Be(5);
        savedTrainer.CommissionEligible.Should().BeTrue();
        savedTrainer.Certification.Should().Contain("ACE CPT");
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
        var handler = new UpdateTrainerCommandHandler(
            Allow(),
            Actor(),
            people,
            users,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<LifeFitAI.Application.Licensing.ILicenseAdminStore>(),
            Substitute.For<LifeFitAI.Application.Platform.IPermissionCacheInvalidator>(),
            Clock());

        var result = await handler.Handle(new UpdateTrainerCommand(trainer.Id, "Leads strength", "Strength", 8, "ACE", null, null, ProfileStatus.Active, "Grace", "Hopper"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Grace");
        result.Value.LastName.Should().Be("Hopper");
        result.Value.Specialization.Should().Be("Strength");
        await people.Received(1).GetTrainerAsync(_tenantId, trainer.Id, Arg.Any<CancellationToken>());
        await people.Received(1).GetStaffByUserAsync(_tenantId, user.Id, Arg.Any<CancellationToken>());
    }

    private AddCoachCommandHandler Handler(IOperationGuard guard, IAccountLinker accounts, IPeopleRepository people, IUserAccountRepository users, IRoleRepository? roles = null, ITenantMembershipRepository? memberships = null, IAccountActivationMailer? activation = null)
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        memberships ??= Substitute.For<ITenantMembershipRepository>();
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
            Substitute.For<ITenantRepository>(),
            Substitute.For<LifeFitAI.Application.Licensing.ILicenseAdminStore>(),
            Substitute.For<LifeFitAI.Application.Platform.IPermissionCacheInvalidator>(),
            activation);
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
