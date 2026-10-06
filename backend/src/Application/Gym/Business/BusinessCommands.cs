using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Business;

public sealed record GetBusinessProfileQuery : IRequest<Result<BusinessProfileResponse>>;

public sealed class GetBusinessProfileQueryHandler : IRequestHandler<GetBusinessProfileQuery, Result<BusinessProfileResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBusinessProfileRepository _profiles;
    private readonly ITenantRepository _tenants;

    public GetBusinessProfileQueryHandler(IOperationGuard guard, ICurrentActor actor, IBusinessProfileRepository profiles, ITenantRepository tenants)
    {
        _guard = guard;
        _actor = actor;
        _profiles = profiles;
        _tenants = tenants;
    }

    public async Task<Result<BusinessProfileResponse>> Handle(GetBusinessProfileQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.BusinessProfileRead, FeatureCodes.BusinessProfile, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<BusinessProfileResponse>(gate.Error);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (profile is null || tenant is null)
        {
            return Result.Failure<BusinessProfileResponse>(GymErrors.NotFound);
        }

        return Result.Success(Map(profile, tenant.Name));
    }

    internal static BusinessProfileResponse Map(BusinessProfile profile, string businessName) =>
        new(profile.Id, profile.TenantId, businessName, profile.LegalName, profile.Description, profile.Email, profile.Phone, profile.Website, profile.Address, profile.City, profile.State, profile.Country, profile.PostalCode, profile.LogoUrl, profile.TimeZone, profile.Currency);
}

public sealed record UpdateBusinessProfileCommand(
    string? LegalName,
    string? Description,
    string? Email,
    string? Phone,
    string? Website,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? LogoUrl,
    string TimeZone,
    string Currency) : IRequest<Result<BusinessProfileResponse>>;

public sealed class UpdateBusinessProfileCommandValidator : AbstractValidator<UpdateBusinessProfileCommand>
{
    public UpdateBusinessProfileCommandValidator()
    {
        RuleFor(command => command.TimeZone).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
    }
}

public sealed class UpdateBusinessProfileCommandHandler : IRequestHandler<UpdateBusinessProfileCommand, Result<BusinessProfileResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBusinessProfileRepository _profiles;
    private readonly ITenantRepository _tenants;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdateBusinessProfileCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IBusinessProfileRepository profiles,
        ITenantRepository tenants,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _profiles = profiles;
        _tenants = tenants;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<BusinessProfileResponse>> Handle(UpdateBusinessProfileCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.BusinessProfileUpdate, FeatureCodes.BusinessProfile, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<BusinessProfileResponse>(gate.Error);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZone, out _))
        {
            return Result.Failure<BusinessProfileResponse>(GymErrors.InvalidState);
        }

        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || !RoleNames.IsBusiness(tenant.TenantType))
        {
            return Result.Failure<BusinessProfileResponse>(GymErrors.NotFound);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var created = profile is null;
        profile ??= BusinessProfile.Create(Guid.NewGuid(), tenantId, request.TimeZone, request.Currency.ToUpperInvariant());
        profile.Update(request.LegalName, request.Description, request.Email, request.Phone, request.Website, request.Address, request.City, request.State, request.Country, request.PostalCode, request.LogoUrl, request.TimeZone, request.Currency.ToUpperInvariant());
        if (created)
        {
            await _profiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, created ? "CreateBusinessProfile" : "UpdateBusinessProfile", "BusinessProfile", profile.Id, null, tenant.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(GetBusinessProfileQueryHandler.Map(profile, tenant.Name));
    }
}
