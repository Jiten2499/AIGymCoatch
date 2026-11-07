using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using LifeFitAI.Infrastructure.Storage;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Storage;

public sealed class GoogleDriveStorageTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StorageKeys_RejectTraversalAndForeignTenant()
    {
        var fileId = Guid.NewGuid();
        StorageKeys.IsSafeLogicalKey(_tenantA, StorageKeys.GymKey(_tenantA, fileId)).Should().BeTrue();
        StorageKeys.IsSafeLogicalKey(_tenantA, StorageKeys.GymKey(_tenantB, fileId)).Should().BeFalse();
        StorageKeys.IsSafeLogicalKey(_tenantA, $"tenants/{_tenantA:D}/../secret").Should().BeFalse();
        StorageKeys.SanitizeFileName("../../evil.png").Should().Be("evil.png");
    }

    [Fact]
    public void DataProtection_EncryptsRefreshToken()
    {
        var provider = DataProtectionProvider.Create("LifeFitAI.Tests");
        var protector = new DataProtectionStorageSecretProtector(provider);
        var protectedValue = protector.Protect("refresh-token-value");
        protectedValue.Should().NotBe("refresh-token-value");
        protector.Unprotect(protectedValue).Should().Be("refresh-token-value");
        protector.Unprotect("not-a-protected-payload").Should().BeNull();
    }

    [Fact]
    public async Task FeatureDisabled_BlocksConnectionAndUpload()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(Result.Failure(GymErrors.FeatureDisabled));
        var query = new StorageConnectionQueryHandler(guard, Actor(_tenantA), Substitute.For<ITenantStorageConnectionRepository>(), OAuth(true));
        (await query.Handle(new GetGoogleDriveConnectionQuery(), CancellationToken.None)).Error.Should().Be(StorageErrors.FeatureDisabled);

        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantA, FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(false);
        var files = new FileStorageService(features, Substitute.For<ITenantStorageConnectionRepository>(), OAuth(true), Substitute.For<IGoogleDriveApiClient>(), Protector(), Audit(), Unit(), Clock());
        var upload = await files.UploadAsync(new FileUploadRequest(_tenantA, _userId, StorageKeys.GymKey(_tenantA, Guid.NewGuid()), "logo.png", "image/png", new byte[] { 1, 2, 3 }, "Gym", null), CancellationToken.None);
        upload.Error.Should().Be(StorageErrors.FeatureDisabled);
    }

    [Fact]
    public async Task OAuthState_RejectsReplayAndMismatch()
    {
        var repo = Substitute.For<ITenantStorageConnectionRepository>();
        repo.GetOAuthStateByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((StorageOAuthState?)null);
        var handler = Handler(repo, OAuth(true), Substitute.For<IGoogleDriveApiClient>());
        (await handler.Handle(new CompleteGoogleDriveOAuthCommand("code", "state", null), CancellationToken.None)).Error.Should().Be(StorageErrors.OAuthInvalid);

        var state = StorageOAuthState.Create(Guid.NewGuid(), _tenantA, _userId, Hash("once"), "verifier", StorageOAuthClientKind.Web, null, _utc, _utc.AddMinutes(10));
        state.TryConsume(_utc).Should().BeTrue();
        repo.GetOAuthStateByHashAsync(Hash("once"), Arg.Any<CancellationToken>()).Returns(state);
        (await handler.Handle(new CompleteGoogleDriveOAuthCommand("code", "once", null), CancellationToken.None)).Error.Should().Be(StorageErrors.OAuthInvalid);
    }

    [Fact]
    public async Task TenantIsolation_FileLookupUsesActorTenant()
    {
        var repo = Substitute.For<ITenantStorageConnectionRepository>();
        repo.GetFileAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((StoredFile?)null);
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantB, FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(true);
        var files = new FileStorageService(features, repo, OAuth(true), Substitute.For<IGoogleDriveApiClient>(), Protector(), Audit(), Unit(), Clock());
        (await files.GetMetadataAsync(_tenantB, Guid.NewGuid(), CancellationToken.None)).Error.Should().Be(StorageErrors.NotFound);
        await repo.DidNotReceive().GetFileAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_RequiresConnectedDriveAndPersistsMetadata()
    {
        var connection = TenantStorageConnection.Create(Guid.NewGuid(), _tenantA, StorageProviderKind.GoogleDrive);
        connection.Connect("biz@example.com", "protected", "drive.file", "root", "members", "comms", "gym", "other", _userId, _utc);
        var repo = Substitute.For<ITenantStorageConnectionRepository>();
        repo.GetAsync(_tenantA, StorageProviderKind.GoogleDrive, Arg.Any<CancellationToken>()).Returns(connection);
        repo.GetFileByLogicalKeyAsync(_tenantA, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((StoredFile?)null);
        var oauth = OAuth(true);
        oauth.RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new GoogleTokenSet("access", null, "drive.file", 3600)));
        var drive = Substitute.For<IGoogleDriveApiClient>();
        drive.UploadAsync("access", "gym", "logo.png", "image/png", Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GoogleDriveUploadedFile("ext-1", "gym", "logo.png", 3)));
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantA, FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(true);
        var secrets = Substitute.For<IStorageSecretProtector>();
        secrets.Unprotect("protected").Returns("refresh");
        var files = new FileStorageService(features, repo, oauth, drive, secrets, Audit(), Unit(), Clock());
        var key = StorageKeys.GymKey(_tenantA, Guid.NewGuid());
        var result = await files.UploadAsync(new FileUploadRequest(_tenantA, _userId, key, "logo.png", "image/png", new byte[] { 1, 2, 3 }, "Gym", null), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.LogicalKey.Should().Be(key);
        await repo.Received(1).AddFileAsync(Arg.Is<StoredFile>(file => file.TenantId == _tenantA && file.ExternalFileId == "ext-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Disconnect_ClearsSecrets()
    {
        var connection = TenantStorageConnection.Create(Guid.NewGuid(), _tenantA, StorageProviderKind.GoogleDrive);
        connection.Connect("biz@example.com", "protected", "drive.file", "root", "m", "c", "g", "o", _userId, _utc);
        var repo = Substitute.For<ITenantStorageConnectionRepository>();
        repo.GetAsync(_tenantA, StorageProviderKind.GoogleDrive, Arg.Any<CancellationToken>()).Returns(connection);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.IntegrationManage, FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new StorageConnectionCommandHandler(guard, Actor(_tenantA), repo, OAuth(true), Substitute.For<IGoogleDriveApiClient>(), Protector(), Audit(), Unit(), Clock(), Substitute.For<IClientContext>());
        var result = await handler.Handle(new DisconnectGoogleDriveCommand(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        connection.Status.Should().Be(StorageConnectionStatus.NotConnected);
        connection.ProtectedRefreshToken.Should().BeNull();
        connection.ProgressFolderId.Should().BeNull();
    }

    [Fact]
    public async Task ProgressPhotos_UploadToTheConnectedTenantFolder_AndStopWhenRevoked()
    {
        var tenant = Guid.NewGuid();
        var member = Guid.NewGuid();
        var assessment = Guid.NewGuid();
        var photo = Guid.NewGuid();
        var key = $"tenants/{tenant:D}/members/{member:D}/progress/{assessment:D}/{photo:D}";
        StorageKeys.IsSafeLogicalKey(tenant, key).Should().BeTrue();
        StorageKeys.CategoryFromKey(key).Should().Be("progress");

        var connection = TenantStorageConnection.Create(Guid.NewGuid(), tenant, StorageProviderKind.GoogleDrive);
        connection.Connect("biz@example.com", "protected", "drive.file", "root", "members", "comms", "gym", "other", _userId, _utc);
        connection.SetProgressFolder("folder-9", "Progress");
        connection.FolderForCategory("progress").Should().Be("folder-9");
        var repo = Substitute.For<ITenantStorageConnectionRepository>();
        repo.GetAsync(tenant, StorageProviderKind.GoogleDrive, Arg.Any<CancellationToken>()).Returns(connection);
        repo.GetFileByLogicalKeyAsync(tenant, key, Arg.Any<CancellationToken>()).Returns((StoredFile?)null);
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(tenant, FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(true);
        var files = Substitute.For<IFileStorageService>();
        files.UploadAsync(Arg.Is<FileUploadRequest>(item => item.TenantId == tenant && item.EntityId == assessment && item.LogicalKey == key), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StoredFileResponse(Guid.NewGuid(), "GoogleDrive", "photo.jpg", "image/jpeg", 4, key, "MemberProgressAssessment", assessment, "Active", _utc)));
        var local = Substitute.For<IFileStorage>();
        var archive = new ProgressPhotoArchive(features, repo, files, local);
        var saved = await archive.SaveAsync(tenant, _userId, assessment, photo, key, "image/jpeg", new byte[] { 1, 2, 3, 4 }, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();
        await local.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());

        var oauth = OAuth(true);
        oauth.RefreshAsync("refresh", Arg.Any<CancellationToken>()).Returns(Result.Success(new GoogleTokenSet("access", null, "drive.file", 3600)));
        var drive = Substitute.For<IGoogleDriveApiClient>();
        drive.UploadAsync("access", "folder-9", Arg.Any<string>(), "image/jpeg", Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GoogleDriveUploadedFile("ext-photo", "folder-9", "photo.jpg", 4)));
        var secrets = Substitute.For<IStorageSecretProtector>();
        secrets.Unprotect("protected").Returns("refresh");
        var service = new FileStorageService(features, repo, oauth, drive, secrets, Audit(), Unit(), Clock());
        var persisted = await service.UploadAsync(new FileUploadRequest(tenant, _userId, key, "photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 }, "MemberProgressAssessment", assessment), CancellationToken.None);
        persisted.IsSuccess.Should().BeTrue();
        await repo.Received().AddFileAsync(Arg.Is<StoredFile>(file => file.TenantId == tenant && file.ExternalFileId == "ext-photo" && file.EntityId == assessment), Arg.Any<CancellationToken>());

        connection.MarkRevoked();
        var blocked = await archive.SaveAsync(tenant, _userId, assessment, photo, key, "image/jpeg", new byte[] { 1 }, CancellationToken.None);
        blocked.Error.Should().Be(StorageErrors.OAuthRevoked);
        await files.Received(1).UploadAsync(Arg.Any<FileUploadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FolderChange_UsesTheActorTenantAndRejectsAnotherTenantFile()
    {
        var connection = TenantStorageConnection.Create(Guid.NewGuid(), _tenantA, StorageProviderKind.GoogleDrive);
        connection.Connect("biz@example.com", "protected", "drive.file", "root", "members", "comms", "gym", "other", _userId, _utc);
        var repo = Substitute.For<ITenantStorageConnectionRepository>();
        repo.GetAsync(_tenantA, StorageProviderKind.GoogleDrive, Arg.Any<CancellationToken>()).Returns(connection);
        var oauth = OAuth(true);
        oauth.RefreshAsync("refresh", Arg.Any<CancellationToken>()).Returns(Result.Success(new GoogleTokenSet("access", null, "drive.file", 3600)));
        var drive = Substitute.For<IGoogleDriveApiClient>();
        drive.DescribeFolderAsync("access", "folder-9", Arg.Any<CancellationToken>()).Returns(Result.Success(new GoogleDriveFolder("folder-9", "Front desk")));
        var secrets = Substitute.For<IStorageSecretProtector>();
        secrets.Unprotect("protected").Returns("refresh");
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.IntegrationManage, FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new GoogleDriveSettingsHandler(guard, Actor(_tenantA), repo, oauth, drive, secrets, Audit(), Unit(), Clock(), Substitute.For<IClientContext>());
        var changed = await handler.Handle(new ChangeGoogleDriveFolderCommand("folder-9"), CancellationToken.None);
        changed.IsSuccess.Should().BeTrue();
        changed.Value.ProgressFolderName.Should().Be("Front desk");
        connection.TenantId.Should().Be(_tenantA);
        await repo.DidNotReceive().GetAsync(_tenantB, Arg.Any<StorageProviderKind>(), Arg.Any<CancellationToken>());

        var toggled = await handler.Handle(new UpdateProgressPhotoStorageCommand(false), CancellationToken.None);
        toggled.Value.StoreProgressPhotos.Should().BeFalse();
    }

    [Fact]
    public async Task Authorization_ForbiddenWithoutPermission()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.IntegrationManage, FeatureCodes.GoogleDriveStorage, Arg.Any<CancellationToken>()).Returns(Result.Failure(IdentityErrors.Forbidden));
        var handler = new StorageConnectionCommandHandler(guard, Actor(_tenantA), Substitute.For<ITenantStorageConnectionRepository>(), OAuth(true), Substitute.For<IGoogleDriveApiClient>(), Protector(), Audit(), Unit(), Clock(), Substitute.For<IClientContext>());
        (await handler.Handle(new BeginGoogleDriveConnectionCommand("Web", null), CancellationToken.None)).Error.Should().Be(IdentityErrors.Forbidden);
    }

    private StorageConnectionCommandHandler Handler(ITenantStorageConnectionRepository repo, IGoogleDriveOAuthService oauth, IGoogleDriveApiClient drive)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return new StorageConnectionCommandHandler(guard, Actor(_tenantA), repo, oauth, drive, Protector(), Audit(), Unit(), Clock(), Substitute.For<IClientContext>());
    }

    private ICurrentActor Actor(Guid tenantId)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(tenantId);
        actor.UserId.Returns(_userId);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }

    private static IUnitOfWork Unit()
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return unit;
    }

    private static IAuditLogRepository Audit() => Substitute.For<IAuditLogRepository>();

    private static IStorageSecretProtector Protector()
    {
        var secrets = Substitute.For<IStorageSecretProtector>();
        secrets.Protect(Arg.Any<string>()).Returns(call => $"p:{call.Arg<string>()}");
        secrets.Unprotect(Arg.Any<string>()).Returns(call => call.Arg<string>().StartsWith("p:", StringComparison.Ordinal) ? call.Arg<string>()[2..] : null);
        return secrets;
    }

    private static IGoogleDriveOAuthService OAuth(bool configured)
    {
        var oauth = Substitute.For<IGoogleDriveOAuthService>();
        oauth.IsConfigured.Returns(configured);
        oauth.WebSuccessRedirectUri.Returns("http://localhost:3000/business/integrations");
        oauth.MobileSuccessRedirectUri.Returns("lifefit://integrations/google-drive");
        oauth.BuildAuthorizationUrl(Arg.Any<string>(), Arg.Any<string>()).Returns("https://accounts.google.com/o/oauth2/v2/auth?x=1");
        return oauth;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
