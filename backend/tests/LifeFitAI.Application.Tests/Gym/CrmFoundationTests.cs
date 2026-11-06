using System.Security.Claims;
using FluentAssertions;
using LifeFitAI.Api.Middleware;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Crm;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
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

public sealed class CrmFoundationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userA = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 6, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StatusTransitions_AllowTheSalesPath_AndRejectConvertedJumps()
    {
        var lead = Lead.Create(Guid.NewGuid(), _tenantA, "Ada", "+15551212", null, LeadSource.WalkIn, null);
        lead.ChangeStatus(LeadStatus.Contacted).Should().BeTrue();
        lead.ChangeStatus(LeadStatus.FollowUp).Should().BeTrue();
        lead.ChangeStatus(LeadStatus.Trial).Should().BeTrue();
        lead.ChangeStatus(LeadStatus.Converted).Should().BeFalse();
        lead.Convert(Guid.NewGuid(), _utc, _userA).Should().BeTrue();
        lead.ConvertedByUserId.Should().Be(_userA);
        lead.ChangeStatus(LeadStatus.Lost).Should().BeFalse();
        lead.Status.Should().Be(LeadStatus.Converted);
    }

    [Fact]
    public async Task TenantIsolation_CoversLeadsFollowUpsTrialsSearchAndSummary()
    {
        await using var db = CreateDb();
        var repo = new LeadRepository(db);
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Annual", null, 365, MembershipType.Yearly, null, 100, "USD");
        var otherPlan = MembershipPlan.Create(Guid.NewGuid(), _tenantB, "Other", null, 30, MembershipType.Monthly, null, 10, "USD");
        var lead = Lead.Create(Guid.NewGuid(), _tenantA, "Ada Prospect", "+15551212", "ada@gym.test", LeadSource.Instagram, "note");
        lead.Update(lead.FullName, lead.Mobile, lead.Email, lead.Source, plan.Id, lead.Notes);
        var other = Lead.Create(Guid.NewGuid(), _tenantB, "Other", "+15559999", null, LeadSource.Google, null);
        Stamp(db, plan, otherPlan, lead, other);
        db.MembershipPlans.AddRange(plan, otherPlan);
        db.Leads.AddRange(lead, other);
        var followUp = LeadFollowUp.Create(Guid.NewGuid(), _tenantA, lead.Id, _utc, "Called", null, _utc);
        var trial = LeadTrial.Create(Guid.NewGuid(), _tenantA, lead.Id, _utc.AddDays(1), "Trial", _utc);
        db.LeadFollowUps.Add(followUp);
        db.LeadTrials.Add(trial);
        await db.SaveChangesAsync();

        (await repo.GetAsync(_tenantB, lead.Id, CancellationToken.None)).Should().BeNull();
        (await repo.ReadAsync(_tenantB, lead.Id, CancellationToken.None)).Should().BeNull();
        (await repo.GetFollowUpAsync(_tenantB, lead.Id, followUp.Id, CancellationToken.None)).Should().BeNull();
        (await repo.GetTrialAsync(_tenantB, lead.Id, trial.Id, CancellationToken.None)).Should().BeNull();
        (await repo.HasOpenMobileAsync(_tenantA, "+15551212", null, CancellationToken.None)).Should().BeTrue();
        (await repo.HasOpenMobileAsync(_tenantB, "+15551212", null, CancellationToken.None)).Should().BeFalse();
        var page = await repo.SearchAsync(_tenantA, new LeadSearch("Ada", null, LeadSource.Instagram, null, plan.Id, null, null, null, null, 1, 20), CancellationToken.None);
        page.TotalCount.Should().Be(1);
        page.Items[0].InterestedPackage.Should().Be("Annual");
        var empty = await repo.SearchAsync(_tenantA, new LeadSearch(null, null, null, null, null, null, null, null, null, 2, 20), CancellationToken.None);
        empty.Items.Should().BeEmpty();
        var summaryA = await repo.SummarizeAsync(_tenantA, _utc.Date, _utc.Date.AddDays(1), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var summaryB = await repo.SummarizeAsync(_tenantB, _utc.Date, _utc.Date.AddDays(1), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        summaryA.TotalLeads.Should().Be(1);
        summaryB.TotalLeads.Should().Be(1);
        summaryA.TrialsScheduled.Should().Be(1);
        summaryB.TrialsScheduled.Should().Be(0);
    }

    [Fact]
    public async Task FollowUpQueue_UsesTheBusinessTimezone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "IST", "IST");
        var today = CrmCalendar.LocalDayStartUtc(_utc, zone);
        var next = CrmCalendar.NextLocalDayStartUtc(_utc, zone);
        today.Should().Be(new DateTime(2026, 10, 6, 18, 30, 0, DateTimeKind.Utc));
        next.Should().Be(today.AddDays(1));

        await using var db = CreateDb();
        var repo = new LeadRepository(db);
        var lead = Lead.Create(Guid.NewGuid(), _tenantA, "Ada", "+15551212", null, LeadSource.Phone, null);
        Stamp(db, lead);
        db.Leads.Add(lead);
        db.LeadFollowUps.Add(LeadFollowUp.Create(Guid.NewGuid(), _tenantA, lead.Id, today.AddHours(1), "today", null, _utc));
        db.LeadFollowUps.Add(LeadFollowUp.Create(Guid.NewGuid(), _tenantA, lead.Id, today.AddHours(-1), "overdue", null, _utc));
        db.LeadFollowUps.Add(LeadFollowUp.Create(Guid.NewGuid(), _tenantB, Guid.NewGuid(), today.AddHours(1), "other", null, _utc));
        db.LeadFollowUps.Add(LeadFollowUp.Create(Guid.NewGuid(), _tenantA, lead.Id, next.AddHours(1), "upcoming", null, _utc));
        await db.SaveChangesAsync();

        (await repo.QueueAsync(_tenantA, "today", today, next, new PageQuery(1, 20), CancellationToken.None)).TotalCount.Should().Be(1);
        (await repo.QueueAsync(_tenantA, "overdue", today, next, new PageQuery(1, 20), CancellationToken.None)).TotalCount.Should().Be(1);
        (await repo.QueueAsync(_tenantA, "upcoming", today, next, new PageQuery(1, 20), CancellationToken.None)).TotalCount.Should().Be(1);
        (await repo.QueueAsync(_tenantB, "today", today, next, new PageQuery(1, 20), CancellationToken.None)).TotalCount.Should().Be(0);
    }

    [Fact]
    public void ConversionAndLoss_PreserveHistory()
    {
        var lead = Lead.Create(Guid.NewGuid(), _tenantA, "Ada", "+15551212", null, LeadSource.Referral, "keep");
        var memberId = Guid.NewGuid();
        lead.Convert(memberId, _utc, _userA).Should().BeTrue();
        lead.ConvertedMemberId.Should().Be(memberId);
        lead.ConvertedByUserId.Should().Be(_userA);
        lead.Source.Should().Be(LeadSource.Referral);
        lead.Notes.Should().Be("keep");
        lead.Convert(Guid.NewGuid(), _utc, _userA).Should().BeFalse();

        var lost = Lead.Create(Guid.NewGuid(), _tenantA, "Bea", "+15551313", null, LeadSource.Other, null);
        lost.MarkLost(LeadLostReason.TooExpensive, _userA, _utc).Should().BeTrue();
        lost.Reopen().Should().BeTrue();
        lost.Status.Should().Be(LeadStatus.New);
        lost.LostReason.Should().Be(LeadLostReason.TooExpensive);
        lost.LostAt.Should().Be(_utc);
        lost.LostByUserId.Should().Be(_userA);
    }

    [Fact]
    public async Task Handlers_RejectCrossTenantPackagePersonAndMember_AndSkipQueriesWhenDenied()
    {
        var leads = Substitute.For<ILeadRepository>();
        var plans = Substitute.For<IMembershipRepository>();
        var members = Substitute.For<ITenantMembershipRepository>();
        var people = Substitute.For<IPeopleRepository>();
        var denied = new GetLeadQueryHandler(Guard(GymErrors.FeatureDisabled), Actor(), leads);
        (await denied.Handle(new GetLeadQuery(Guid.NewGuid()), CancellationToken.None)).Error.Should().Be(GymErrors.FeatureDisabled);
        var forbidden = new GetLeadsQueryHandler(Guard(IdentityErrors.Forbidden), Actor(), leads, Substitute.For<IBusinessProfileRepository>(), Clock());
        (await forbidden.Handle(new GetLeadsQuery(null, null, null, null, null, null, null, 1, 20, null), CancellationToken.None)).Error.Should().Be(IdentityErrors.Forbidden);
        await leads.DidNotReceive().ReadAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await leads.DidNotReceive().SearchAsync(Arg.Any<Guid>(), Arg.Any<LeadSearch>(), Arg.Any<CancellationToken>());

        plans.GetPlanAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MembershipPlan?)null);
        var create = new CreateLeadCommandHandler(Guard(null), Actor(), leads, plans, members, Audit(), Unit(), Clock(), Client());
        var package = await create.Handle(new CreateLeadCommand("Ada", "+15551212", null, LeadSource.Website, Guid.NewGuid(), null, null, null), CancellationToken.None);
        package.Error.Should().Be(GymErrors.NotFound);
        await leads.DidNotReceive().AddAsync(Arg.Any<Lead>(), Arg.Any<CancellationToken>());

        members.GetActiveAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TenantMember?)null);
        var assign = new AssignLeadCommandHandler(Guard(null), Actor(), leads, members, Audit(), Unit(), Clock(), Client());
        leads.GetAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Lead.Create(Guid.NewGuid(), _tenantA, "Ada", "+15551212", null, LeadSource.Facebook, null));
        var salesperson = await assign.Handle(new AssignLeadCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        salesperson.Error.Should().Be(GymErrors.NotFound);

        people.GetMemberAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        var convert = ConvertHandler(leads, people, plans, Substitute.For<IBranchRepository>());
        var missingMember = await convert.Handle(new ConvertLeadCommand(Guid.NewGuid(), Guid.NewGuid(), "A", "B", "9876543210", null, null, null, null, null, null, new DateOnly(2026, 10, 6), null, null, null, null, null), CancellationToken.None);
        missingMember.Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task Convert_CreatesMemberWithoutLogin_LinksExisting_AndAssignsOptionalPackage()
    {
        var leads = Substitute.For<ILeadRepository>();
        var people = Substitute.For<IPeopleRepository>();
        var plans = Substitute.For<IMembershipRepository>();
        var branches = Substitute.For<IBranchRepository>();
        var join = new DateOnly(2026, 10, 6);
        var open = Lead.Create(Guid.NewGuid(), _tenantA, "Rahul Shah", "9876543210", "rahul@example.com", LeadSource.WalkIn, "notes");
        leads.GetAsync(_tenantA, open.Id, Arg.Any<CancellationToken>()).Returns(open);
        leads.ReadAsync(_tenantA, open.Id, Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var lead = open;
            return new LeadDetails(lead.Id, lead.FullName, lead.Mobile, lead.Email, lead.Source, lead.Status, null, null, null, null, null, lead.Notes, lead.ConvertedMemberId, lead.ConvertedAt, null, null, _utc, lead.ConvertedByUserId, "Ada Owner");
        });
        MemberProfile? saved = null;
        people.AddMemberAsync(Arg.Do<MemberProfile>(profile => saved = profile), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var create = ConvertHandler(leads, people, plans, branches);
        (await create.Handle(new ConvertLeadCommand(open.Id, null, "Rahul", "Shah", "9876543210", "rahul@example.com", null, null, null, null, null, join, null, null, null, null, null), CancellationToken.None)).IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.UserId.Should().BeNull();
        open.Status.Should().Be(LeadStatus.Converted);
        open.ConvertedMemberId.Should().Be(saved.Id);
        open.ConvertedByUserId.Should().Be(_userA);

        var linkedLead = Lead.Create(Guid.NewGuid(), _tenantA, "Bea Lee", "9123456789", null, LeadSource.Phone, null);
        var existing = MemberProfile.Create(Guid.NewGuid(), _tenantA, null, "MEXIST01", join, "Bea", "Lee", "9123456789");
        leads.GetAsync(_tenantA, linkedLead.Id, Arg.Any<CancellationToken>()).Returns(linkedLead);
        leads.ReadAsync(_tenantA, linkedLead.Id, Arg.Any<CancellationToken>()).Returns(new LeadDetails(linkedLead.Id, linkedLead.FullName, linkedLead.Mobile, null, linkedLead.Source, LeadStatus.Converted, null, null, null, null, null, null, existing.Id, _utc, null, null, _utc, _userA, null));
        people.GetMemberAsync(_tenantA, existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        (await create.Handle(new ConvertLeadCommand(linkedLead.Id, existing.Id, "Bea", "Lee", "9123456789", null, null, null, null, null, null, join, null, null, null, null, null), CancellationToken.None)).IsSuccess.Should().BeTrue();
        linkedLead.ConvertedMemberId.Should().Be(existing.Id);

        var packageLead = Lead.Create(Guid.NewGuid(), _tenantA, "Cara Day", "9000000001", null, LeadSource.Google, null);
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Monthly", null, 30, MembershipType.Monthly, null, 100m, "INR", PackageCategory.Standard, PackageDurationUnit.Month, 1);
        leads.GetAsync(_tenantA, packageLead.Id, Arg.Any<CancellationToken>()).Returns(packageLead);
        leads.ReadAsync(_tenantA, packageLead.Id, Arg.Any<CancellationToken>()).Returns(new LeadDetails(packageLead.Id, packageLead.FullName, packageLead.Mobile, null, packageLead.Source, LeadStatus.Converted, null, null, null, null, null, null, Guid.NewGuid(), _utc, null, null, _utc, _userA, null));
        plans.GetPlanAsync(_tenantA, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        plans.OverlapsMembershipAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>()).Returns(false);
        GymMembership? membership = null;
        plans.AddMembershipAsync(Arg.Do<GymMembership>(item => membership = item), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        (await create.Handle(new ConvertLeadCommand(packageLead.Id, null, "Cara", "Day", "9000000001", null, null, null, null, null, null, join, null, null, plan.Id, null, join), CancellationToken.None)).IsSuccess.Should().BeTrue();
        membership.Should().NotBeNull();
        membership!.BranchId.Should().BeNull();
        membership.EndDate.Should().Be(plan.InclusiveEnd(join));

        plan.Deactivate();
        var inactiveLead = Lead.Create(Guid.NewGuid(), _tenantA, "Dan", "9000000002", null, LeadSource.Other, null);
        leads.GetAsync(_tenantA, inactiveLead.Id, Arg.Any<CancellationToken>()).Returns(inactiveLead);
        plans.GetPlanAsync(_tenantA, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        (await create.Handle(new ConvertLeadCommand(inactiveLead.Id, null, "Dan", "Day", "9000000002", null, null, null, null, null, null, join, null, null, plan.Id, null, join), CancellationToken.None)).Error.Should().Be(GymErrors.InvalidState);

        var foreignPlan = MembershipPlan.Create(Guid.NewGuid(), _tenantB, "Other", null, 30, MembershipType.Monthly, null, 10m, "USD");
        plans.GetPlanAsync(_tenantA, foreignPlan.Id, Arg.Any<CancellationToken>()).Returns((MembershipPlan?)null);
        (await create.Handle(new ConvertLeadCommand(inactiveLead.Id, null, "Dan", "Day", "9000000002", null, null, null, null, null, null, join, null, null, foreignPlan.Id, null, join), CancellationToken.None)).Error.Should().Be(GymErrors.NotFound);

        people.FindMemberMatchesAsync(_tenantA, "9876543210", "rahul@example.com", Arg.Any<CancellationToken>()).Returns([new MemberMatchItem(existing.Id, "Bea Lee", "9123456789", null, "MEXIST01", ProfileStatus.Active, join)]);
        leads.GetAsync(_tenantA, open.Id, Arg.Any<CancellationToken>()).Returns(open);
        var matches = await new MatchLeadMembersQueryHandler(Guard(null), Actor(), leads, people).Handle(new MatchLeadMembersQuery(open.Id, null, null), CancellationToken.None);
        matches.IsSuccess.Should().BeTrue();
        matches.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task Convert_RejectsAlreadyConvertedAndForeignMember()
    {
        var leads = Substitute.For<ILeadRepository>();
        var people = Substitute.For<IPeopleRepository>();
        var plans = Substitute.For<IMembershipRepository>();
        var branches = Substitute.For<IBranchRepository>();
        var join = new DateOnly(2026, 10, 6);
        var converted = Lead.Create(Guid.NewGuid(), _tenantA, "Done", "9111111111", null, LeadSource.WalkIn, null);
        converted.Convert(Guid.NewGuid(), _utc, _userA);
        leads.GetAsync(_tenantA, converted.Id, Arg.Any<CancellationToken>()).Returns(converted);
        var handler = ConvertHandler(leads, people, plans, branches);
        (await handler.Handle(new ConvertLeadCommand(converted.Id, null, "Done", "Person", "9111111111", null, null, null, null, null, null, join, null, null, null, null, null), CancellationToken.None)).Error.Should().Be(GymErrors.InvalidState);

        var open = Lead.Create(Guid.NewGuid(), _tenantA, "Open", "9222222222", null, LeadSource.WalkIn, null);
        leads.GetAsync(_tenantA, open.Id, Arg.Any<CancellationToken>()).Returns(open);
        people.GetMemberAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        (await handler.Handle(new ConvertLeadCommand(open.Id, Guid.NewGuid(), "Open", "Person", "9222222222", null, null, null, null, null, null, join, null, null, null, null, null), CancellationToken.None)).Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public void TrialStatus_ChangesWithoutAttendance()
    {
        var trial = LeadTrial.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), _utc, null, _utc);
        trial.Change(TrialStatus.Completed, "came", "joined interest");
        trial.Status.Should().Be(TrialStatus.Completed);
        trial.Change(TrialStatus.NoShow, trial.Notes, "missed");
        trial.Status.Should().Be(TrialStatus.NoShow);
    }

    [Fact]
    public async Task SuspendedWorkspace_StopsTheLeadRoute()
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
        context.Request.Path = "/api/v1/leads";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", _tenantA.ToString())], "test"));
        await middleware.InvokeAsync(context, tenants, new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())));
        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
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
        actor.UserId.Returns(_userA);
        actor.TenantId.Returns(_tenantA);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }

    private static IAuditLogRepository Audit()
    {
        var audit = Substitute.For<IAuditLogRepository>();
        audit.AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return audit;
    }

    private static IUnitOfWork Unit()
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        return unit;
    }

    private ConvertLeadCommandHandler ConvertHandler(ILeadRepository leads, IPeopleRepository people, IMembershipRepository plans, IBranchRepository branches)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.LicenseManage, Arg.Any<CancellationToken>()).Returns(true);
        return new(Guard(null), Actor(), permissions, leads, people, plans, branches, Substitute.For<IMemberAppSeatAssigner>(), Audit(), Unit(), Clock(), Client());
    }

    private static IClientContext Client() => Substitute.For<IClientContext>();

    private static void Stamp(ApplicationDbContext db, params LifeFitAI.SharedKernel.Primitives.BaseEntity[] entities)
    {
        foreach (var entity in entities)
        {
            db.Entry(entity).Property("RowVersion").CurrentValue = new byte[] { 1 };
        }
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
