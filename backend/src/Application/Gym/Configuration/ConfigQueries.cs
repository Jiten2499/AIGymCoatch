using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Configuration;

public sealed record GetBusinessConfigurationQuery : IRequest<Result<BusinessConfigurationResponse>>;

public sealed class GetBusinessConfigurationQueryHandler : IRequestHandler<GetBusinessConfigurationQuery, Result<BusinessConfigurationResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBusinessConfigRepository _configs;
    private readonly IUnitOfWork _unitOfWork;

    public GetBusinessConfigurationQueryHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _configs = configs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BusinessConfigurationResponse>> Handle(GetBusinessConfigurationQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ConfigurationRead, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<BusinessConfigurationResponse>(allowed.Error);
        }

        await EnsureDefaultsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success(await _configs.ReadAsync(tenantId, cancellationToken).ConfigureAwait(false));
    }

    private async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var changed = false;
        if (await _configs.GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false) is null)
        {
            await _configs.AddConfigAsync(TenantBusinessConfig.CreateDefault(Guid.NewGuid(), tenantId), cancellationToken).ConfigureAwait(false);
            changed = true;
        }

        if ((await _configs.ListCategoriesAsync(tenantId, false, cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            foreach (var name in new[] { "Student", "Women", "Corporate", "Senior Citizen", "Defence", "Employee", "Referral" })
            {
                await _configs.AddCategoryAsync(MemberCategory.Create(Guid.NewGuid(), tenantId, name, null), cancellationToken).ConfigureAwait(false);
            }

            changed = true;
        }

        if ((await _configs.ListLeadSourcesAsync(tenantId, cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            foreach (var (code, name) in new[] { ("Instagram", "Instagram"), ("Google", "Google"), ("WalkIn", "Walk-in"), ("Referral", "Referral"), ("Facebook", "Facebook"), ("Website", "Website"), ("Phone", "Phone"), ("Advertisement", "Advertisement"), ("Other", "Other") })
            {
                await _configs.AddLeadSourceAsync(CrmLeadSource.Create(Guid.NewGuid(), tenantId, name, code, true), cancellationToken).ConfigureAwait(false);
            }

            changed = true;
        }

        if ((await _configs.ListLostReasonsAsync(tenantId, cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            foreach (var (code, name) in new[] { ("TooExpensive", "Too expensive"), ("JoinedAnotherGym", "Joined another gym"), ("NotInterested", "Not interested"), ("NoResponse", "No response"), ("Location", "Location"), ("Timing", "Timing"), ("Other", "Other") })
            {
                await _configs.AddLostReasonAsync(CrmLostReason.Create(Guid.NewGuid(), tenantId, name, code, true), cancellationToken).ConfigureAwait(false);
            }

            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
