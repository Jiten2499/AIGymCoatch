using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Storage;

public sealed record UploadStoredFileCommand(
    string LogicalKey,
    string FileName,
    string ContentType,
    byte[] Content,
    string? EntityType,
    Guid? EntityId) : IRequest<Result<StoredFileResponse>>;

public sealed record GetStoredFileQuery(Guid Id) : IRequest<Result<StoredFileResponse>>;

public sealed record DownloadStoredFileQuery(Guid Id) : IRequest<Result<FileContentResult>>;

public sealed record DeleteStoredFileCommand(Guid Id) : IRequest<Result>;

public sealed class UploadStoredFileCommandValidator : AbstractValidator<UploadStoredFileCommand>
{
    public UploadStoredFileCommandValidator()
    {
        RuleFor(command => command.LogicalKey).NotEmpty().MaximumLength(500);
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255);
        RuleFor(command => command.ContentType).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Content).NotEmpty();
    }
}

public sealed class StorageFileHandler :
    IRequestHandler<UploadStoredFileCommand, Result<StoredFileResponse>>,
    IRequestHandler<GetStoredFileQuery, Result<StoredFileResponse>>,
    IRequestHandler<DownloadStoredFileQuery, Result<FileContentResult>>,
    IRequestHandler<DeleteStoredFileCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFileStorageService _files;

    public StorageFileHandler(IOperationGuard guard, ICurrentActor actor, IFileStorageService files)
    {
        _guard = guard;
        _actor = actor;
        _files = files;
    }

    public async Task<Result<StoredFileResponse>> Handle(UploadStoredFileCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StorageFileWrite, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<StoredFileResponse>(StorageConnectionQueryHandler.MapGate(gate.Error));
        }

        return await _files.UploadAsync(
            new FileUploadRequest(tenantId, userId, request.LogicalKey, request.FileName, request.ContentType, request.Content, request.EntityType, request.EntityId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StoredFileResponse>> Handle(GetStoredFileQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StorageFileRead, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<StoredFileResponse>(StorageConnectionQueryHandler.MapGate(gate.Error));
        }

        return await _files.GetMetadataAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<FileContentResult>> Handle(DownloadStoredFileQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StorageFileRead, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<FileContentResult>(StorageConnectionQueryHandler.MapGate(gate.Error));
        }

        return await _files.DownloadAsync(new FileDownloadRequest(tenantId, request.Id), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> Handle(DeleteStoredFileCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StorageFileWrite, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure(StorageConnectionQueryHandler.MapGate(gate.Error));
        }

        return await _files.DeleteAsync(tenantId, request.Id, userId, cancellationToken).ConfigureAwait(false);
    }
}
