using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Attendance;

public sealed record CheckInCommand(Guid? MemberProfileId, Guid BranchId, CheckInMethod Method, string? QrToken) : IRequest<Result<AttendanceResponse>>;

public sealed class CheckInCommandValidator : AbstractValidator<CheckInCommand>
{
    public CheckInCommandValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.Method).IsInEnum();
        RuleFor(command => command.QrToken).NotEmpty().When(command => command.Method == CheckInMethod.QR);
    }
}

public sealed class CheckInCommandHandler : IRequestHandler<CheckInCommand, Result<AttendanceResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly IMembershipRepository _memberships;
    private readonly IAttendanceRepository _attendance;
    private readonly ISecretTokenFactory _tokens;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CheckInCommandHandler(IOperationGuard guard, ICurrentActor actor, IPermissionService permissions, IPeopleRepository people, IBranchRepository branches, IMembershipRepository memberships, IAttendanceRepository attendance, ISecretTokenFactory tokens, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _people = people;
        _branches = branches;
        _memberships = memberships;
        _attendance = attendance;
        _tokens = tokens;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<AttendanceResponse>> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var feature = request.Method == CheckInMethod.QR ? FeatureCodes.QrCheckIn : FeatureCodes.Attendance;
        var gate = await _guard.AllowAsync(PermissionCodes.AttendanceCheckIn, feature, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<AttendanceResponse>(gate.Error);
        }

        var memberId = request.MemberProfileId;
        QrCheckInToken? token = null;
        if (request.Method == CheckInMethod.QR)
        {
            token = await _attendance.GetTokenByHashAsync(_tokens.Hash(request.QrToken!), cancellationToken).ConfigureAwait(false);
            if (token is null || token.TenantId != tenantId || !token.IsUsable(_clock.UtcNow))
            {
                return Result.Failure<AttendanceResponse>(GymErrors.InvalidToken);
            }

            memberId = token.MemberProfileId;
        }

        if (memberId is not Guid profileId)
        {
            return Result.Failure<AttendanceResponse>(GymErrors.NotFound);
        }

        var member = await _people.GetMemberAsync(tenantId, profileId, cancellationToken).ConfigureAwait(false);
        if (member is null || member.Status != ProfileStatus.Active)
        {
            return Result.Failure<AttendanceResponse>(GymErrors.NotFound);
        }

        var canCheckInOthers = await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.MemberRead, cancellationToken).ConfigureAwait(false);
        if (!canCheckInOthers && member.UserId != actorId)
        {
            return Result.Failure<AttendanceResponse>(IdentityErrors.Forbidden);
        }

        var branch = await _branches.GetAsync(tenantId, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (branch is null || branch.Status != BranchStatus.Active)
        {
            return Result.Failure<AttendanceResponse>(GymErrors.NotFound);
        }

        var page = await _memberships.ListMembershipsAsync(tenantId, GymMembershipStatus.Active, null, branch.Id, null, null, new PageQuery(1, 20), cancellationToken).ConfigureAwait(false);
        var summary = page.Items.FirstOrDefault(item => item.MemberProfileId == member.Id);
        var membership = summary is null ? null : await _memberships.GetMembershipAsync(tenantId, summary.Id, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        if (membership is null || !membership.CanCheckIn(today))
        {
            return Result.Failure<AttendanceResponse>(GymErrors.InvalidCheckIn);
        }

        var plan = await _memberships.GetPlanAsync(tenantId, membership.MembershipPlanId, cancellationToken).ConfigureAwait(false);
        if (plan?.MaxVisits is int maxVisits && await _memberships.CountVisitsAsync(tenantId, membership.Id, cancellationToken).ConfigureAwait(false) >= maxVisits)
        {
            return Result.Failure<AttendanceResponse>(GymErrors.InvalidCheckIn);
        }

        if (await _attendance.GetOpenAsync(tenantId, member.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<AttendanceResponse>(GymErrors.OpenVisit);
        }

        var visit = AttendanceRecord.CheckIn(Guid.NewGuid(), tenantId, branch.Id, member.Id, membership.Id, request.Method, _clock.UtcNow);
        token?.MarkUsed(_clock.UtcNow);
        await _attendance.AddAsync(visit, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "CheckIn", "Attendance", visit.Id, null, request.Method.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new AttendanceResponse(visit.Id, visit.BranchId, visit.MemberProfileId, visit.GymMembershipId, visit.CheckInTime, visit.CheckOutTime, visit.CheckInMethod));
    }
}

public sealed record CheckOutCommand(Guid AttendanceId) : IRequest<Result<AttendanceResponse>>;

public sealed class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, Result<AttendanceResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IPeopleRepository _people;
    private readonly IAttendanceRepository _attendance;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CheckOutCommandHandler(IOperationGuard guard, ICurrentActor actor, IPermissionService permissions, IPeopleRepository people, IAttendanceRepository attendance, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _people = people;
        _attendance = attendance;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<AttendanceResponse>> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.AttendanceCheckOut, FeatureCodes.Attendance, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<AttendanceResponse>(gate.Error);
        }

        var visit = await _attendance.GetAsync(tenantId, request.AttendanceId, cancellationToken).ConfigureAwait(false);
        if (visit is null)
        {
            return Result.Failure<AttendanceResponse>(GymErrors.NotFound);
        }

        var member = await _people.GetMemberAsync(tenantId, visit.MemberProfileId, cancellationToken).ConfigureAwait(false);
        var canCloseOthers = await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.MemberRead, cancellationToken).ConfigureAwait(false);
        if (member is null || (!canCloseOthers && member.UserId != actorId))
        {
            return Result.Failure<AttendanceResponse>(IdentityErrors.Forbidden);
        }

        if (!visit.CheckOut(_clock.UtcNow))
        {
            return Result.Failure<AttendanceResponse>(GymErrors.InvalidState);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "CheckOut", "Attendance", visit.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new AttendanceResponse(visit.Id, visit.BranchId, visit.MemberProfileId, visit.GymMembershipId, visit.CheckInTime, visit.CheckOutTime, visit.CheckInMethod));
    }
}

public sealed record IssueQrTokenCommand(Guid MemberProfileId) : IRequest<Result<QrTokenResponse>>;

public sealed class IssueQrTokenCommandHandler : IRequestHandler<IssueQrTokenCommand, Result<QrTokenResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IPeopleRepository _people;
    private readonly IAttendanceRepository _attendance;
    private readonly ISecretTokenFactory _tokens;
    private readonly IQrSettings _settings;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public IssueQrTokenCommandHandler(IOperationGuard guard, ICurrentActor actor, IPermissionService permissions, IPeopleRepository people, IAttendanceRepository attendance, ISecretTokenFactory tokens, IQrSettings settings, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _people = people;
        _attendance = attendance;
        _tokens = tokens;
        _settings = settings;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<QrTokenResponse>> Handle(IssueQrTokenCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.AttendanceCheckIn, FeatureCodes.QrCheckIn, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<QrTokenResponse>(gate.Error);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberProfileId, cancellationToken).ConfigureAwait(false);
        var canIssueForOthers = await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.MemberRead, cancellationToken).ConfigureAwait(false);
        if (member is null || member.Status != ProfileStatus.Active || (!canIssueForOthers && member.UserId != actorId))
        {
            return Result.Failure<QrTokenResponse>(GymErrors.NotFound);
        }

        var raw = _tokens.Create();
        var now = _clock.UtcNow;
        var token = QrCheckInToken.Issue(Guid.NewGuid(), tenantId, member.Id, _tokens.Hash(raw), now, now.Add(_settings.Lifetime), actorId);
        await _attendance.AddTokenAsync(token, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "IssueQrToken", "QrCheckInToken", token.Id, null, null, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new QrTokenResponse(raw, now.Add(_settings.Lifetime)));
    }
}
