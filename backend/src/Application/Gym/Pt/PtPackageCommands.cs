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

public sealed record PtPackageResponse(Guid Id, string Name, string? Description, int SessionCount, int ValidityDays, decimal Price, string Currency, bool IsActive, string Status);

public sealed record ListPtPackagesQuery(bool ActiveOnly, string? Term = null, string? Status = null) : IRequest<Result<IReadOnlyList<PtPackageResponse>>>;

public sealed class ListPtPackagesQueryHandler : IRequestHandler<ListPtPackagesQuery, Result<IReadOnlyList<PtPackageResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPtPackageRepository _packages;

    public ListPtPackagesQueryHandler(IOperationGuard guard, ICurrentActor actor, IPtPackageRepository packages)
    {
        _guard = guard;
        _actor = actor;
        _packages = packages;
    }

    public async Task<Result<IReadOnlyList<PtPackageResponse>>> Handle(ListPtPackagesQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<PtPackageResponse>>(gate.Error);
        }

        var items = await _packages.ListAsync(tenantId, request.ActiveOnly, cancellationToken).ConfigureAwait(false);
        var term = request.Term?.Trim();
        var filtered = items.Where(package =>
            (string.IsNullOrWhiteSpace(term) || package.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(request.Status) || string.Equals(package.Status.ToString(), request.Status, StringComparison.OrdinalIgnoreCase)));
        return Result.Success<IReadOnlyList<PtPackageResponse>>(filtered.Select(Map).ToArray());
    }

    internal static PtPackageResponse Map(PtPackage package) =>
        new(package.Id, package.Name, package.Description, package.SessionCount, package.ValidityDays, package.Price, package.Currency, package.IsActive, package.Status.ToString());
}

public sealed record CreatePtPackageCommand(string Name, string? Description, int SessionCount, int ValidityDays, decimal Price, string Currency, string? Status = null) : IRequest<Result<PtPackageResponse>>;

public sealed class CreatePtPackageCommandValidator : AbstractValidator<CreatePtPackageCommand>
{
    public CreatePtPackageCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.SessionCount).GreaterThan(0);
        RuleFor(command => command.ValidityDays).GreaterThan(0);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
        RuleFor(command => command.Status).Must(status => status is null or "Active" or "Draft").WithMessage("A new package can be Draft or Active.");
    }
}

public sealed class CreatePtPackageCommandHandler : IRequestHandler<CreatePtPackageCommand, Result<PtPackageResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPtPackageRepository _packages;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreatePtPackageCommandHandler(IOperationGuard guard, ICurrentActor actor, IPtPackageRepository packages, IBusinessProfileRepository profiles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
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

    public async Task<Result<PtPackageResponse>> Handle(CreatePtPackageCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<PtPackageResponse>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        if (await _packages.NameExistsAsync(tenantId, request.Name.Trim(), cancellationToken).ConfigureAwait(false))
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
            var package = PtPackage.Create(Guid.NewGuid(), tenantId, request.Name, request.Description, request.SessionCount, request.ValidityDays, request.Price, currency.Value);
            if (string.Equals(request.Status, nameof(PtPackageStatus.Draft), StringComparison.OrdinalIgnoreCase))
            {
                package.MarkDraft();
            }

            await _packages.AddAsync(package, cancellationToken).ConfigureAwait(false);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "PtPackageCreated", "PtPackage", package.Id, null, package.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ListPtPackagesQueryHandler.Map(package));
        }
        catch (ArgumentException)
        {
            return Result.Failure<PtPackageResponse>(GymErrors.InvalidState);
        }
    }
}
