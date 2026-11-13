using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Storage;

public sealed record SaveGoogleDriveAppCommand(string ClientId, string? ClientSecret, string RedirectUri) : IRequest<Result<StorageConnectionResponse>>;

public sealed class SaveGoogleDriveAppCommandValidator : AbstractValidator<SaveGoogleDriveAppCommand>
{
    public SaveGoogleDriveAppCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ClientSecret).MaximumLength(500);
        RuleFor(command => command.RedirectUri)
            .NotEmpty()
            .MaximumLength(500)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.Scheme is "https" or "http")
            .WithMessage("Redirect URI must be an absolute http or https address.");
    }
}

public sealed class GoogleDriveAppHandler : IRequestHandler<SaveGoogleDriveAppCommand, Result<StorageConnectionResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ITenantStorageConnectionRepository _storage;
    private readonly IGoogleDriveOAuthService _oauth;
    private readonly IStorageSecretProtector _secrets;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public GoogleDriveAppHandler(
        ICurrentActor actor,
        IRoleRepository roles,
        IPermissionService permissions,
        ITenantStorageConnectionRepository storage,
        IGoogleDriveOAuthService oauth,
        IStorageSecretProtector secrets,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _storage = storage;
        _oauth = oauth;
        _secrets = secrets;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<StorageConnectionResponse>> Handle(SaveGoogleDriveAppCommand request, CancellationToken cancellationToken)
    {
        var gate = await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<StorageConnectionResponse>(gate.Error);
        }

        var connection = await _storage.GetPlatformAsync(StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            connection = PlatformStorageConnection.Create(Guid.NewGuid(), StorageProviderKind.GoogleDrive);
            await _storage.AddPlatformAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var protectedSecret = string.IsNullOrWhiteSpace(request.ClientSecret) ? null : _secrets.Protect(request.ClientSecret.Trim());
        connection.SetApp(request.ClientId, request.RedirectUri, protectedSecret);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), _actor.TenantId, _actor.UserId, "GoogleDriveAppSaved", "PlatformStorageConnection", connection.Id, null, connection.ClientId, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var canConnect = await _oauth.HasAppAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(StorageMaps.ToResponse(connection, canConnect, _oauth.RedirectUri));
    }
}
