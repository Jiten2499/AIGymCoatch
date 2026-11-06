using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

/// <summary>
/// Gym member LifeFit access: basic login is independent of optional app-plan seats.
/// HasBasicAccess = linked User login. HasAppPlan = active SubscriptionSeat for premium features.
/// </summary>
public sealed record MemberAppAccessResponse(
    Guid MemberProfileId,
    Guid? UserId,
    bool HasBasicAccess,
    bool HasAppPlan,
    string BasicStatus,
    string PlanStatus,
    Guid? SeatId,
    Guid? SubscriptionId,
    string? PlanCode,
    string? PlanName,
    DateTime? AssignedAt,
    int? AvailableSeats)
{
    /// <summary>Backward-compatible alias: basic LifeFit login, not a paid seat.</summary>
    public bool HasAppAccess => HasBasicAccess;
}

public sealed record GetMemberAppAccessQuery(Guid MemberProfileId) : IRequest<Result<MemberAppAccessResponse>>;

public sealed record EnableMemberBasicAccessCommand(Guid MemberProfileId, string Email) : IRequest<Result<MemberAppAccessResponse>>;

public sealed record AssignMemberAppPlanCommand(Guid MemberProfileId, Guid SubscriptionId, string? Email = null) : IRequest<Result<MemberAppAccessResponse>>;

public sealed record RemoveMemberAppPlanCommand(Guid MemberProfileId) : IRequest<Result>;

public sealed class EnableMemberBasicAccessCommandValidator : AbstractValidator<EnableMemberBasicAccessCommand>
{
    public EnableMemberBasicAccessCommandValidator()
    {
        RuleFor(command => command.MemberProfileId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
    }
}

public sealed class AssignMemberAppPlanCommandValidator : AbstractValidator<AssignMemberAppPlanCommand>
{
    public AssignMemberAppPlanCommandValidator()
    {
        RuleFor(command => command.MemberProfileId).NotEmpty();
        RuleFor(command => command.SubscriptionId).NotEmpty();
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
    }
}

public interface IMemberAppSeatAssigner
{
    Task<Result<MemberAppAccessResponse>> EnableBasicAccessAsync(
        Guid tenantId,
        Guid actorId,
        MemberProfile member,
        string email,
        CancellationToken cancellationToken);

    Task<Result<MemberAppAccessResponse>> AssignAsync(
        Guid tenantId,
        Guid actorId,
        MemberProfile member,
        Guid subscriptionId,
        string? emailToLink,
        CancellationToken cancellationToken);

    Task<Result> ReleaseAsync(Guid tenantId, Guid actorId, MemberProfile member, CancellationToken cancellationToken);

    Task<MemberAppAccessResponse> ReadAsync(Guid tenantId, MemberProfile member, CancellationToken cancellationToken);
}

public sealed class GetMemberAppAccessQueryHandler : IRequestHandler<GetMemberAppAccessQuery, Result<MemberAppAccessResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IMemberAppSeatAssigner _seats;

    public GetMemberAppAccessQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IMemberAppSeatAssigner seats)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _seats = seats;
    }

    public async Task<Result<MemberAppAccessResponse>> Handle(GetMemberAppAccessQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MemberAppAccessResponse>(gate.Error);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberProfileId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<MemberAppAccessResponse>(GymErrors.NotFound);
        }

        return Result.Success(await _seats.ReadAsync(tenantId, member, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class EnableMemberBasicAccessCommandHandler : IRequestHandler<EnableMemberBasicAccessCommand, Result<MemberAppAccessResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IMemberAppSeatAssigner _seats;
    private readonly IUnitOfWork _unitOfWork;

    public EnableMemberBasicAccessCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IPeopleRepository people,
        IMemberAppSeatAssigner seats,
        IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _seats = seats;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MemberAppAccessResponse>> Handle(EnableMemberBasicAccessCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<MemberAppAccessResponse>(gate.Error);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberProfileId, cancellationToken).ConfigureAwait(false);
        if (member is null || member.Status != ProfileStatus.Active)
        {
            return Result.Failure<MemberAppAccessResponse>(GymErrors.NotFound);
        }

        MemberAppAccessResponse? response = null;
        Error? error = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var enabled = await _seats.EnableBasicAccessAsync(tenantId, actorId, member, request.Email, ct).ConfigureAwait(false);
            if (enabled.IsFailure)
            {
                error = enabled.Error;
                return;
            }

            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            response = enabled.Value;
        }, cancellationToken).ConfigureAwait(false);

        return error is not null
            ? Result.Failure<MemberAppAccessResponse>(error)
            : Result.Success(response!);
    }
}

public sealed class AssignMemberAppPlanCommandHandler : IRequestHandler<AssignMemberAppPlanCommand, Result<MemberAppAccessResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IOperationGuard _guard;
    private readonly IPeopleRepository _people;
    private readonly IMemberAppSeatAssigner _seats;
    private readonly IUnitOfWork _unitOfWork;

    public AssignMemberAppPlanCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        IOperationGuard guard,
        IPeopleRepository people,
        IMemberAppSeatAssigner seats,
        IUnitOfWork unitOfWork)
    {
        _actor = actor;
        _permissions = permissions;
        _guard = guard;
        _people = people;
        _seats = seats;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MemberAppAccessResponse>> Handle(AssignMemberAppPlanCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<MemberAppAccessResponse>(gate.Error);
        }

        if (!await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<MemberAppAccessResponse>(IdentityErrors.Forbidden);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberProfileId, cancellationToken).ConfigureAwait(false);
        if (member is null || member.Status != ProfileStatus.Active)
        {
            return Result.Failure<MemberAppAccessResponse>(GymErrors.NotFound);
        }

        MemberAppAccessResponse? response = null;
        Error? error = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var assigned = await _seats.AssignAsync(tenantId, actorId, member, request.SubscriptionId, request.Email, ct).ConfigureAwait(false);
            if (assigned.IsFailure)
            {
                error = assigned.Error;
                return;
            }

            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            response = assigned.Value;
        }, cancellationToken).ConfigureAwait(false);

        return error is not null
            ? Result.Failure<MemberAppAccessResponse>(error)
            : Result.Success(response!);
    }
}

public sealed class RemoveMemberAppPlanCommandHandler : IRequestHandler<RemoveMemberAppPlanCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IOperationGuard _guard;
    private readonly IPeopleRepository _people;
    private readonly IMemberAppSeatAssigner _seats;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveMemberAppPlanCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        IOperationGuard guard,
        IPeopleRepository people,
        IMemberAppSeatAssigner seats,
        IUnitOfWork unitOfWork)
    {
        _actor = actor;
        _permissions = permissions;
        _guard = guard;
        _people = people;
        _seats = seats;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RemoveMemberAppPlanCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return gate.IsFailure ? gate : Result.Failure(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberProfileId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _seats.ReleaseAsync(tenantId, actorId, member, ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
