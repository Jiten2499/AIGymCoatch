using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Pt;

public sealed record GetPtPackageQuery(Guid Id) : IRequest<Result<PtPackageResponse>>;

public sealed class GetPtPackageQueryHandler : IRequestHandler<GetPtPackageQuery, Result<PtPackageResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPtPackageRepository _packages;

    public GetPtPackageQueryHandler(IOperationGuard guard, ICurrentActor actor, IPtPackageRepository packages)
    {
        _guard = guard;
        _actor = actor;
        _packages = packages;
    }

    public async Task<Result<PtPackageResponse>> Handle(GetPtPackageQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PtPackageResponse>(gate.Error);
        }

        var package = await _packages.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return package is null
            ? Result.Failure<PtPackageResponse>(GymErrors.NotFound)
            : Result.Success(ListPtPackagesQueryHandler.Map(package));
    }
}

public sealed record UpdatePtPackageCommand(Guid Id, string Name, string? Description, int SessionCount, int ValidityDays, decimal Price, string Currency) : IRequest<Result<PtPackageResponse>>;

public sealed class UpdatePtPackageCommandValidator : AbstractValidator<UpdatePtPackageCommand>
{
    public UpdatePtPackageCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.SessionCount).GreaterThan(0);
        RuleFor(command => command.ValidityDays).GreaterThan(0);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
    }
}

public sealed class UpdatePtPackageCommandHandler : IRequestHandler<UpdatePtPackageCommand, Result<PtPackageResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPtPackageRepository _packages;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdatePtPackageCommandHandler(IOperationGuard guard, ICurrentActor actor, IPtPackageRepository packages, IBusinessProfileRepository profiles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _packages = packages;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PtPackageResponse>> Handle(UpdatePtPackageCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<PtPackageResponse>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        var package = await _packages.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (package is null)
        {
            return Result.Failure<PtPackageResponse>(GymErrors.NotFound);
        }

        if (!string.Equals(package.Name, request.Name.Trim(), StringComparison.Ordinal)
            && await _packages.NameExistsAsync(tenantId, request.Name.Trim(), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PtPackageResponse>(GymErrors.Conflict);
        }

        var currency = await PtPackageCurrency.ResolveAsync(_profiles, tenantId, request.Currency, cancellationToken).ConfigureAwait(false);
        if (currency.IsFailure)
        {
            return Result.Failure<PtPackageResponse>(currency.Error);
        }

        try
        {
            package.Update(request.Name, request.Description, request.SessionCount, request.ValidityDays, request.Price, currency.Value);
        }
        catch (ArgumentException)
        {
            return Result.Failure<PtPackageResponse>(GymErrors.InvalidState);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "PtPackageUpdated", "PtPackage", package.Id, null, package.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ListPtPackagesQueryHandler.Map(package));
    }
}

public sealed record SetPtPackageStatusCommand(Guid Id, string Status) : IRequest<Result<PtPackageResponse>>;

public sealed class SetPtPackageStatusCommandValidator : AbstractValidator<SetPtPackageStatusCommand>
{
    public SetPtPackageStatusCommandValidator()
    {
        RuleFor(command => command.Status).Must(status => status is "Active" or "Inactive");
    }
}

public sealed class SetPtPackageStatusCommandHandler : IRequestHandler<SetPtPackageStatusCommand, Result<PtPackageResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPtPackageRepository _packages;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public SetPtPackageStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, IPtPackageRepository packages, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _packages = packages;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PtPackageResponse>> Handle(SetPtPackageStatusCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<PtPackageResponse>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        var package = await _packages.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (package is null)
        {
            return Result.Failure<PtPackageResponse>(GymErrors.NotFound);
        }

        if (string.Equals(request.Status, nameof(PtPackageStatus.Active), StringComparison.OrdinalIgnoreCase))
        {
            package.Activate();
        }
        else
        {
            package.Deactivate();
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "PtPackageStatusChanged", "PtPackage", package.Id, null, package.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ListPtPackagesQueryHandler.Map(package));
    }
}
