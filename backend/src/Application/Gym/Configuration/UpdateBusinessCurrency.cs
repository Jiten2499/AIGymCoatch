using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Configuration;

public interface ITenantCurrencyStore
{
    Task ApplyAsync(Guid tenantId, string currency, CancellationToken cancellationToken);
}

public sealed record UpdateBusinessCurrencyCommand(string Currency) : IRequest<Result<string>>;

public sealed class UpdateBusinessCurrencyCommandValidator : AbstractValidator<UpdateBusinessCurrencyCommand>
{
    public UpdateBusinessCurrencyCommandValidator() =>
        RuleFor(command => command.Currency).NotEmpty().Matches("^[A-Za-z]{3}$");
}

public sealed class UpdateBusinessCurrencyCommandHandler : IRequestHandler<UpdateBusinessCurrencyCommand, Result<string>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITenantCurrencyStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdateBusinessCurrencyCommandHandler(IOperationGuard guard, ICurrentActor actor, ITenantCurrencyStore store, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<string>> Handle(UpdateBusinessCurrencyCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ConfigurationManage, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<string>(allowed.Error);
        }

        var currency = request.Currency.Trim().ToUpperInvariant();
        if (!CurrencyCodes.IsKnown(currency))
        {
            return Result.Failure<string>(GymErrors.UnknownCurrency);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _store.ApplyAsync(tenantId, currency, token).ConfigureAwait(false);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "BusinessCurrencyUpdated", "BusinessProfile", tenantId, null, currency, _client.IpAddress, _client.UserAgent, _clock.UtcNow), token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return Result.Success(currency);
    }
}
