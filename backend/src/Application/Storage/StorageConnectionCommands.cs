using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Storage;

public sealed record GetGoogleDriveConnectionQuery : IRequest<Result<StorageConnectionResponse>>;

public sealed record BeginGoogleDriveConnectionCommand(string ClientKind, string? ReturnPath) : IRequest<Result<BeginStorageConnectionResponse>>;

public sealed record CompleteGoogleDriveOAuthCommand(string? Code, string? State, string? Error) : IRequest<Result<string>>;

public sealed record TestGoogleDriveConnectionCommand : IRequest<Result<StorageConnectionResponse>>;

public sealed record DisconnectGoogleDriveCommand : IRequest<Result<StorageConnectionResponse>>;

public sealed class BeginGoogleDriveConnectionCommandValidator : AbstractValidator<BeginGoogleDriveConnectionCommand>
{
    public BeginGoogleDriveConnectionCommandValidator()
    {
        RuleFor(command => command.ClientKind).Must(kind => kind is "Web" or "Mobile");
        RuleFor(command => command.ReturnPath).MaximumLength(500);
    }
}

public sealed class StorageConnectionQueryHandler : IRequestHandler<GetGoogleDriveConnectionQuery, Result<StorageConnectionResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITenantStorageConnectionRepository _storage;
    private readonly IGoogleDriveOAuthService _oauth;

    public StorageConnectionQueryHandler(IOperationGuard guard, ICurrentActor actor, ITenantStorageConnectionRepository storage, IGoogleDriveOAuthService oauth)
    {
        _guard = guard;
        _actor = actor;
        _storage = storage;
        _oauth = oauth;
    }

    public async Task<Result<StorageConnectionResponse>> Handle(GetGoogleDriveConnectionQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.IntegrationRead, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<StorageConnectionResponse>(MapGate(gate.Error));
        }

        var connection = await _storage.GetAsync(tenantId, StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        return Result.Success(StorageMaps.ToResponse(connection, _oauth.IsConfigured));
    }

    internal static Error MapGate(Error error) =>
        error.Code == GymErrors.FeatureDisabled.Code ? StorageErrors.FeatureDisabled : error;
}

public sealed class StorageConnectionCommandHandler :
    IRequestHandler<BeginGoogleDriveConnectionCommand, Result<BeginStorageConnectionResponse>>,
    IRequestHandler<CompleteGoogleDriveOAuthCommand, Result<string>>,
    IRequestHandler<TestGoogleDriveConnectionCommand, Result<StorageConnectionResponse>>,
    IRequestHandler<DisconnectGoogleDriveCommand, Result<StorageConnectionResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITenantStorageConnectionRepository _storage;
    private readonly IGoogleDriveOAuthService _oauth;
    private readonly IGoogleDriveApiClient _drive;
    private readonly IStorageSecretProtector _secrets;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public StorageConnectionCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ITenantStorageConnectionRepository storage,
        IGoogleDriveOAuthService oauth,
        IGoogleDriveApiClient drive,
        IStorageSecretProtector secrets,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _storage = storage;
        _oauth = oauth;
        _drive = drive;
        _secrets = secrets;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<BeginStorageConnectionResponse>> Handle(BeginGoogleDriveConnectionCommand request, CancellationToken cancellationToken)
    {
        var gate = await AllowManageAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<BeginStorageConnectionResponse>(gate.Error);
        }

        if (!_oauth.IsConfigured)
        {
            return Result.Failure<BeginStorageConnectionResponse>(StorageErrors.ConfigurationMissing);
        }

        var tenantId = _actor.TenantId!.Value;
        var userId = _actor.UserId!.Value;
        var connection = await EnsureConnectionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        connection.MarkConnecting();

        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = CreateCodeChallenge(verifier);
        var expiresAt = _clock.UtcNow.AddMinutes(10);
        var oauthState = StorageOAuthState.Create(
            Guid.NewGuid(),
            tenantId,
            userId,
            Hash(state),
            verifier,
            request.ClientKind == "Mobile" ? StorageOAuthClientKind.Mobile : StorageOAuthClientKind.Web,
            request.ReturnPath,
            _clock.UtcNow,
            expiresAt);
        await _storage.AddOAuthStateAsync(oauthState, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "GoogleDriveConnectStarted", "TenantStorageConnection", connection.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new BeginStorageConnectionResponse(_oauth.BuildAuthorizationUrl(state, challenge), expiresAt));
    }

    public async Task<Result<string>> Handle(CompleteGoogleDriveOAuthCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Error))
        {
            return Result.Failure<string>(StorageErrors.OAuthDenied);
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State))
        {
            return Result.Failure<string>(StorageErrors.OAuthInvalid);
        }

        var oauthState = await _storage.GetOAuthStateByHashAsync(Hash(request.State), cancellationToken).ConfigureAwait(false);
        if (oauthState is null || !oauthState.TryConsume(_clock.UtcNow))
        {
            return Result.Failure<string>(StorageErrors.OAuthInvalid);
        }

        var tokens = await _oauth.ExchangeCodeAsync(request.Code, oauthState.CodeVerifier, cancellationToken).ConfigureAwait(false);
        if (tokens.IsFailure || string.IsNullOrWhiteSpace(tokens.Value.RefreshToken))
        {
            return Result.Failure<string>(tokens.IsFailure ? tokens.Error : StorageErrors.OAuthInvalid);
        }

        var account = await _drive.GetAccountAsync(tokens.Value.AccessToken, cancellationToken).ConfigureAwait(false);
        if (account.IsFailure)
        {
            return Result.Failure<string>(account.Error);
        }

        var folders = await _drive.EnsureLifeFitFoldersAsync(tokens.Value.AccessToken, cancellationToken).ConfigureAwait(false);
        if (folders.IsFailure)
        {
            return Result.Failure<string>(folders.Error);
        }

        var connection = await EnsureConnectionAsync(oauthState.TenantId, cancellationToken).ConfigureAwait(false);
        connection.Connect(
            account.Value.Email,
            _secrets.Protect(tokens.Value.RefreshToken),
            tokens.Value.Scope ?? "https://www.googleapis.com/auth/drive.file",
            folders.Value.RootFolderId,
            folders.Value.MembersFolderId,
            folders.Value.CommunicationsFolderId,
            folders.Value.GymFolderId,
            folders.Value.OtherFolderId,
            oauthState.UserId,
            _clock.UtcNow);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), oauthState.TenantId, oauthState.UserId, "GoogleDriveConnected", "TenantStorageConnection", connection.Id, null, account.Value.Email, null, null, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(BuildReturnUrl(oauthState, true, null));
    }

    public async Task<Result<StorageConnectionResponse>> Handle(TestGoogleDriveConnectionCommand request, CancellationToken cancellationToken)
    {
        var gate = await AllowManageAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<StorageConnectionResponse>(gate.Error);
        }

        var tenantId = _actor.TenantId!.Value;
        var connection = await _storage.GetAsync(tenantId, StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.Status != StorageConnectionStatus.Connected || string.IsNullOrWhiteSpace(connection.ProtectedRefreshToken) || string.IsNullOrWhiteSpace(connection.RootFolderId))
        {
            return Result.Failure<StorageConnectionResponse>(StorageErrors.NotConnected);
        }

        var refreshToken = _secrets.Unprotect(connection.ProtectedRefreshToken);
        if (refreshToken is null)
        {
            connection.MarkFailed("Stored credentials could not be read.");
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<StorageConnectionResponse>(StorageErrors.OAuthRevoked);
        }

        var tokens = await _oauth.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);
        if (tokens.IsFailure)
        {
            connection.MarkRevoked(tokens.Error.Description);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "GoogleDriveRevoked", "TenantStorageConnection", connection.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<StorageConnectionResponse>(StorageErrors.OAuthRevoked);
        }

        var validated = await _drive.ValidateFolderAsync(tokens.Value.AccessToken, connection.RootFolderId, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
        {
            connection.MarkFailed(validated.Error.Description);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<StorageConnectionResponse>(validated.Error);
        }

        connection.MarkValidated(_clock.UtcNow);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "GoogleDriveTested", "TenantStorageConnection", connection.Id, null, connection.AccountEmail, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(StorageMaps.ToResponse(connection, _oauth.IsConfigured));
    }

    public async Task<Result<StorageConnectionResponse>> Handle(DisconnectGoogleDriveCommand request, CancellationToken cancellationToken)
    {
        var gate = await AllowManageAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<StorageConnectionResponse>(gate.Error);
        }

        var tenantId = _actor.TenantId!.Value;
        var connection = await _storage.GetAsync(tenantId, StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Success(StorageMaps.ToResponse(null, _oauth.IsConfigured));
        }

        connection.Disconnect();
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "GoogleDriveDisconnected", "TenantStorageConnection", connection.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(StorageMaps.ToResponse(connection, _oauth.IsConfigured));
    }

    private async Task<Result> AllowManageAsync(CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.IntegrationManage, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure(StorageConnectionQueryHandler.MapGate(gate.Error));
        }

        return _actor.TenantId is not null && _actor.UserId is not null
            ? Result.Success()
            : Result.Failure(IdentityErrors.InvalidCredentials);
    }

    private async Task<TenantStorageConnection> EnsureConnectionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _storage.GetAsync(tenantId, StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = TenantStorageConnection.Create(Guid.NewGuid(), tenantId, StorageProviderKind.GoogleDrive);
        await _storage.AddAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }

    private string BuildReturnUrl(StorageOAuthState state, bool success, string? error)
    {
        if (!string.IsNullOrWhiteSpace(state.ReturnPath) && Uri.TryCreate(state.ReturnPath, UriKind.Absolute, out var custom))
        {
            var builder = new UriBuilder(custom);
            var query = $"status={(success ? "connected" : "failed")}";
            if (!string.IsNullOrWhiteSpace(error))
            {
                query += $"&error={Uri.EscapeDataString(error)}";
            }

            builder.Query = query;
            return builder.Uri.ToString();
        }

        var baseUri = state.ClientKind == StorageOAuthClientKind.Mobile
            ? _oauth.MobileSuccessRedirectUri
            : _oauth.WebSuccessRedirectUri;
        var separator = baseUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUri}{separator}status={(success ? "connected" : "failed")}";
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
