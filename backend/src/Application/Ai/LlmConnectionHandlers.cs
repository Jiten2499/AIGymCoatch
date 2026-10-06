using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Ai;

public sealed record CreateLlmConnectionCommand(string Name, string Provider, string BaseUrl, string ApiKey, string? DefaultModel, bool IsActive, bool IsDefault, int TimeoutSeconds) : IRequest<Result<LlmConnectionResponse>>;

public sealed class CreateLlmConnectionCommandValidator : AbstractValidator<CreateLlmConnectionCommand>
{
    public CreateLlmConnectionCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Provider).Equal(LlmConnection.OpenAiCompatible);
        RuleFor(command => command.BaseUrl).Must(LlmAddress.IsSafe).WithMessage("The endpoint must be an https address, or http on localhost.");
        RuleFor(command => command.ApiKey).NotEmpty().MaximumLength(500);
        RuleFor(command => command.DefaultModel).MaximumLength(200);
        RuleFor(command => command.TimeoutSeconds).InclusiveBetween(1, 60);
    }
}

public sealed record UpdateLlmConnectionCommand(Guid Id, string Name, string BaseUrl, string? ApiKey, string? DefaultModel, bool IsActive, bool IsDefault, int TimeoutSeconds) : IRequest<Result<LlmConnectionResponse>>;

public sealed class UpdateLlmConnectionCommandValidator : AbstractValidator<UpdateLlmConnectionCommand>
{
    public UpdateLlmConnectionCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.BaseUrl).Must(LlmAddress.IsSafe).WithMessage("The endpoint must be an https address, or http on localhost.");
        RuleFor(command => command.ApiKey).MaximumLength(500);
        RuleFor(command => command.DefaultModel).MaximumLength(200);
        RuleFor(command => command.TimeoutSeconds).InclusiveBetween(1, 60);
    }
}

public sealed record ListLlmConnectionsQuery : IRequest<Result<IReadOnlyList<LlmConnectionResponse>>>;

public sealed record SetDefaultLlmConnectionCommand(Guid Id) : IRequest<Result<LlmConnectionResponse>>;

public sealed record DeleteLlmConnectionCommand(Guid Id) : IRequest<Result>;

public sealed class LlmConnectionHandler :
    IRequestHandler<CreateLlmConnectionCommand, Result<LlmConnectionResponse>>,
    IRequestHandler<UpdateLlmConnectionCommand, Result<LlmConnectionResponse>>,
    IRequestHandler<ListLlmConnectionsQuery, Result<IReadOnlyList<LlmConnectionResponse>>>,
    IRequestHandler<SetDefaultLlmConnectionCommand, Result<LlmConnectionResponse>>,
    IRequestHandler<DeleteLlmConnectionCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ILlmConnectionStore _store;
    private readonly ILlmSecretProtector _secrets;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public LlmConnectionHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ILlmConnectionStore store, ILlmSecretProtector secrets, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _store = store;
        _secrets = secrets;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LlmConnectionResponse>> Handle(CreateLlmConnectionCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<LlmConnectionResponse>(denied.Error);
        }

        var connection = LlmConnection.Create(Guid.NewGuid(), request.Name, request.BaseUrl, _secrets.Protect(request.ApiKey), request.DefaultModel, request.IsActive, request.IsDefault, request.TimeoutSeconds);
        if (connection.IsDefault)
        {
            await _store.ClearOtherDefaultsAsync(connection.Id, cancellationToken).ConfigureAwait(false);
        }

        await _store.AddAsync(connection, cancellationToken).ConfigureAwait(false);
        await AuditAsync("LlmConnectionCreated", connection.Id, connection.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapAsync(connection, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<LlmConnectionResponse>> Handle(UpdateLlmConnectionCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<LlmConnectionResponse>(denied.Error);
        }

        var connection = await _store.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<LlmConnectionResponse>(LlmErrors.NotFound);
        }

        if (connection.IsDefault && (!request.IsActive || !request.IsDefault))
        {
            return Result.Failure<LlmConnectionResponse>(LlmErrors.DefaultRequired);
        }

        connection.Update(request.Name, request.BaseUrl, request.DefaultModel, request.IsActive, request.IsDefault, request.TimeoutSeconds);
        if (connection.IsDefault)
        {
            await _store.ClearOtherDefaultsAsync(connection.Id, cancellationToken).ConfigureAwait(false);
        }

        await AuditAsync("LlmConnectionUpdated", connection.Id, connection.Name, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            connection.ReplaceSecret(_secrets.Protect(request.ApiKey));
            await AuditAsync("LlmSecretReplaced", connection.Id, "LLM connection secret updated", cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapAsync(connection, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<LlmConnectionResponse>>> Handle(ListLlmConnectionsQuery request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<IReadOnlyList<LlmConnectionResponse>>(denied.Error);
        }

        var counts = await _store.ModelCountsAsync(cancellationToken).ConfigureAwait(false);
        var items = (await _store.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(connection => Map(connection, counts.GetValueOrDefault(connection.Id)))
            .ToArray();
        return Result.Success<IReadOnlyList<LlmConnectionResponse>>(items);
    }

    public async Task<Result<LlmConnectionResponse>> Handle(SetDefaultLlmConnectionCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<LlmConnectionResponse>(denied.Error);
        }

        var connection = await _store.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<LlmConnectionResponse>(LlmErrors.NotFound);
        }

        if (!connection.IsActive)
        {
            return Result.Failure<LlmConnectionResponse>(LlmErrors.DefaultRequired);
        }

        await _store.ClearOtherDefaultsAsync(connection.Id, cancellationToken).ConfigureAwait(false);
        connection.SetDefault(true);
        await AuditAsync("LlmDefaultChanged", connection.Id, connection.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapAsync(connection, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> Handle(DeleteLlmConnectionCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var connection = await _store.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure(LlmErrors.NotFound);
        }

        if (connection.IsDefault)
        {
            return Result.Failure(LlmErrors.DefaultRequired);
        }

        connection.SetDefault(false);
        connection.SoftDelete(_clock.UtcNow);
        await AuditAsync("LlmConnectionDeleted", connection.Id, connection.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<LlmConnectionResponse> MapAsync(LlmConnection connection, CancellationToken cancellationToken)
    {
        var counts = await _store.ModelCountsAsync(cancellationToken).ConfigureAwait(false);
        return Map(connection, counts.GetValueOrDefault(connection.Id));
    }

    private static LlmConnectionResponse Map(LlmConnection connection, int modelCount) =>
        new(connection.Id, connection.Name, connection.Provider, connection.BaseUrl, connection.ProtectedApiKey.Length > 0, connection.DefaultModel, connection.IsActive, connection.IsDefault, connection.TimeoutSeconds, connection.LastTestedAt, connection.IsActive ? connection.HealthStatus : "Disabled", connection.LastLatencyMs, modelCount, connection.CreatedAt, connection.UpdatedAt);

    private Task AuditAsync(string action, Guid id, string value, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), null, _actor.UserId, action, "LlmConnection", id, null, value, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);
}

public static class LlmAddress
{
    public static bool IsSafe(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return uri.Scheme == Uri.UriSchemeHttp && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1");
    }
}
