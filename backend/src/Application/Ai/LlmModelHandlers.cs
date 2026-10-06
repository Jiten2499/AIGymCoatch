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

public sealed record ListLlmModelsQuery(Guid ConnectionId) : IRequest<Result<IReadOnlyList<LlmModelResponse>>>;

public sealed record CreateLlmModelCommand(Guid ConnectionId, string ModelIdentifier, string DisplayName, bool IsActive, bool IsDefault) : IRequest<Result<LlmModelResponse>>;

public sealed class CreateLlmModelCommandValidator : AbstractValidator<CreateLlmModelCommand>
{
    public CreateLlmModelCommandValidator()
    {
        RuleFor(command => command.ModelIdentifier).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200);
    }
}

public sealed record UpdateLlmModelCommand(Guid Id, string DisplayName, bool IsActive, bool IsDefault) : IRequest<Result<LlmModelResponse>>;

public sealed class UpdateLlmModelCommandValidator : AbstractValidator<UpdateLlmModelCommand>
{
    public UpdateLlmModelCommandValidator() => RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200);
}

public sealed record DeleteLlmModelCommand(Guid Id) : IRequest<Result>;

public sealed record TestLlmConnectionCommand(Guid Id) : IRequest<Result<LlmTestResponse>>;

public sealed record RefreshLlmModelsCommand(Guid Id) : IRequest<Result<LlmTestResponse>>;

public sealed class LlmModelHandler :
    IRequestHandler<ListLlmModelsQuery, Result<IReadOnlyList<LlmModelResponse>>>,
    IRequestHandler<CreateLlmModelCommand, Result<LlmModelResponse>>,
    IRequestHandler<UpdateLlmModelCommand, Result<LlmModelResponse>>,
    IRequestHandler<DeleteLlmModelCommand, Result>,
    IRequestHandler<TestLlmConnectionCommand, Result<LlmTestResponse>>,
    IRequestHandler<RefreshLlmModelsCommand, Result<LlmTestResponse>>
{
    private static readonly string[] ChatOnly = ["chat"];
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ILlmConnectionStore _store;
    private readonly ILlmSecretProtector _secrets;
    private readonly ILlmProbe _probe;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public LlmModelHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ILlmConnectionStore store, ILlmSecretProtector secrets, ILlmProbe probe, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _store = store;
        _secrets = secrets;
        _probe = probe;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<IReadOnlyList<LlmModelResponse>>> Handle(ListLlmModelsQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<IReadOnlyList<LlmModelResponse>>(denied.Error);
        }

        if (await _store.GetAsync(request.ConnectionId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<LlmModelResponse>>(LlmErrors.NotFound);
        }

        var models = await _store.ListModelsAsync(request.ConnectionId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<LlmModelResponse>>(models.Select(Map).ToArray());
    }

    public async Task<Result<LlmModelResponse>> Handle(CreateLlmModelCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<LlmModelResponse>(denied.Error);
        }

        if (await _store.GetAsync(request.ConnectionId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<LlmModelResponse>(LlmErrors.NotFound);
        }

        if (await _store.ModelIdentifierExistsAsync(request.ConnectionId, request.ModelIdentifier.Trim(), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LlmModelResponse>(new Error("llm.conflict", "That model is already saved for this connection."));
        }

        var model = LlmModel.Create(Guid.NewGuid(), request.ConnectionId, request.ModelIdentifier, request.DisplayName, request.IsActive, request.IsDefault);
        if (model.IsDefault)
        {
            await _store.ClearOtherModelDefaultsAsync(model.ConnectionId, model.Id, cancellationToken).ConfigureAwait(false);
        }

        await _store.AddModelAsync(model, cancellationToken).ConfigureAwait(false);
        await AuditAsync("LlmModelCreated", model.Id, model.ModelIdentifier, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(model));
    }

    public async Task<Result<LlmModelResponse>> Handle(UpdateLlmModelCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<LlmModelResponse>(denied.Error);
        }

        var model = await _store.GetModelAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result.Failure<LlmModelResponse>(LlmErrors.NotFound);
        }

        model.Update(request.DisplayName, request.IsActive, request.IsDefault);
        if (model.IsDefault)
        {
            await _store.ClearOtherModelDefaultsAsync(model.ConnectionId, model.Id, cancellationToken).ConfigureAwait(false);
        }

        await AuditAsync("LlmModelUpdated", model.Id, model.ModelIdentifier, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(model));
    }

    public async Task<Result> Handle(DeleteLlmModelCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var model = await _store.GetModelAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result.Failure(LlmErrors.NotFound);
        }

        model.SetDefault(false);
        model.SoftDelete(_clock.UtcNow);
        await AuditAsync("LlmModelDeleted", model.Id, model.ModelIdentifier, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public Task<Result<LlmTestResponse>> Handle(TestLlmConnectionCommand request, CancellationToken cancellationToken) =>
        Probe(request.Id, "LlmConnectionTested", cancellationToken);

    public Task<Result<LlmTestResponse>> Handle(RefreshLlmModelsCommand request, CancellationToken cancellationToken) =>
        Probe(request.Id, "LlmModelsRefreshed", cancellationToken);

    private async Task<Result<LlmTestResponse>> Probe(Guid id, string action, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<LlmTestResponse>(denied.Error);
        }

        var connection = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<LlmTestResponse>(LlmErrors.NotFound);
        }

        var key = _secrets.Unprotect(connection.ProtectedApiKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            connection.RecordTest(false, 0, _clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(new LlmTestResponse(false, "Failed", null, [], LlmErrors.SecretUnreadable.Description));
        }

        var probe = await _probe.ProbeAsync(connection.BaseUrl, key, connection.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
        connection.RecordTest(probe.Succeeded, probe.LatencyMs, _clock.UtcNow);
        await AuditAsync(action, connection.Id, probe.Succeeded ? "Healthy" : "Failed", cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new LlmTestResponse(probe.Succeeded, probe.Succeeded ? "Healthy" : "Failed", probe.LatencyMs, probe.ModelIds, probe.Succeeded ? null : "Connection failed. Verify the provider configuration and credentials."));
    }

    private static LlmModelResponse Map(LlmModel model) =>
        new(model.Id, model.ConnectionId, model.ModelIdentifier, model.DisplayName, model.IsActive, model.IsDefault, ChatOnly);

    private Task<Result> Gate(CancellationToken cancellationToken) =>
        PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken);

    private Task AuditAsync(string action, Guid id, string value, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), null, _actor.UserId, action, "LlmConnection", id, null, value, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);
}
