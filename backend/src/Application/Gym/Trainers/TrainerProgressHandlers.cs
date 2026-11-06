using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed class TrainerProgressHandlers :
    IRequestHandler<ListMemberProgressQuery, Result<IReadOnlyList<MemberProgressItem>>>,
    IRequestHandler<SaveMemberProgressCommand, Result<Guid>>,
    IRequestHandler<UploadMemberProgressPhotoCommand, Result<Guid>>,
    IRequestHandler<DeleteMemberProgressPhotoCommand, Result<Guid>>,
    IRequestHandler<ReadMemberProgressPhotoQuery, Result<MemberPhotoFile>>
{
    private readonly ICurrentActor _actor;
    private readonly ITrainerMobileStore _store;
    private readonly IFileStorage _storage;
    private readonly IProgressPhotoArchive _photos;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TrainerProgressHandlers(ICurrentActor actor, ITrainerMobileStore store, IFileStorage storage, IProgressPhotoArchive photos, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _store = store;
        _storage = storage;
        _photos = photos;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<IReadOnlyList<MemberProgressItem>>> Handle(ListMemberProgressQuery request, CancellationToken cancellationToken)
    {
        var scope = await Assigned(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MemberProgressItem>>(scope.Error);
        }

        var rows = await _store.ListProgressAsync(scope.Value.TenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        var photos = await _store.ListProgressPhotosAsync(scope.Value.TenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        var grouped = photos.GroupBy(photo => photo.AssessmentId).ToDictionary(group => group.Key, group => (IReadOnlyList<Guid>)group.Select(photo => photo.Id).ToArray());
        return Result.Success<IReadOnlyList<MemberProgressItem>>(TrainerProgressMap.Items(rows, grouped, scope.Value.TrainerId));
    }

    public async Task<Result<Guid>> Handle(SaveMemberProgressCommand request, CancellationToken cancellationToken)
    {
        var scope = await Assigned(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<Guid>(scope.Error);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var member = await _store.GetMemberAsync(scope.Value.TenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        if (request.AssessmentId is Guid assessmentId)
        {
            return await Revise(request, scope.Value, member.UserId, assessmentId, today, cancellationToken).ConfigureAwait(false);
        }

        var created = MemberProgressAssessment.Record(Guid.NewGuid(), scope.Value.TenantId, member.Id, scope.Value.TrainerId, Capture(request), today);
        if (created is null)
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        await Mirror(created, member.UserId, cancellationToken).ConfigureAwait(false);
        await _store.AddProgressAsync(created, cancellationToken).ConfigureAwait(false);
        await Audit("MemberProgressRecorded", created.Id, request.AssessmentDate.ToString("yyyy-MM-dd"), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Id);
    }

    public async Task<Result<Guid>> Handle(UploadMemberProgressPhotoCommand request, CancellationToken cancellationToken)
    {
        var scope = await Assigned(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<Guid>(scope.Error);
        }

        if (!MemberFieldRules.TryContentType(request.Content, request.ContentType, out var contentType))
        {
            return Result.Failure<Guid>(GymErrors.InvalidPhoto);
        }

        var assessment = await _store.GetProgressAsync(scope.Value.TenantId, request.MemberId, request.AssessmentId, cancellationToken).ConfigureAwait(false);
        if (assessment is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        var stored = await _store.ListAssessmentPhotosAsync(scope.Value.TenantId, assessment.Id, cancellationToken).ConfigureAwait(false);
        if (stored.Count >= MemberProgressPhoto.MaxCount)
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        var photoId = Guid.NewGuid();
        var key = MemberProgressAssessment.PhotoKey(scope.Value.TenantId, request.MemberId, assessment.Id, photoId);
        var photo = MemberProgressPhoto.Create(photoId, scope.Value.TenantId, request.MemberId, assessment.Id, key, contentType);
        if (photo is null || !_storage.IsSafeKey(key) || !assessment.AttachPhoto(scope.Value.TrainerId, key, contentType))
        {
            return Result.Failure<Guid>(GymErrors.InvalidPhoto);
        }

        var saved = await _photos.SaveAsync(scope.Value.TenantId, _actor.UserId!.Value, assessment.Id, photo.Id, key, contentType, request.Content, cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return Result.Failure<Guid>(saved.Error);
        }

        await _store.AddProgressPhotoAsync(photo, cancellationToken).ConfigureAwait(false);
        await Audit("MemberProgressPhotoAdded", photo.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(photo.Id);
    }

    public async Task<Result<Guid>> Handle(DeleteMemberProgressPhotoCommand request, CancellationToken cancellationToken)
    {
        var scope = await Assigned(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<Guid>(scope.Error);
        }

        var assessment = await _store.GetProgressAsync(scope.Value.TenantId, request.MemberId, request.AssessmentId, cancellationToken).ConfigureAwait(false);
        var photos = assessment is null ? [] : await _store.ListAssessmentPhotosAsync(scope.Value.TenantId, assessment.Id, cancellationToken).ConfigureAwait(false);
        var photo = photos.SingleOrDefault(item => item.Id == request.PhotoId);
        if (assessment is null || photo is null || !MemberProgressAssessment.OwnsPhoto(scope.Value.TenantId, request.MemberId, assessment.Id, photo.StorageKey))
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        if (assessment.TrainerProfileId != scope.Value.TrainerId)
        {
            return Result.Failure<Guid>(IdentityErrors.Forbidden);
        }

        photo.SoftDelete(_clock.UtcNow);
        var remaining = photos.LastOrDefault(item => item.Id != photo.Id);
        assessment.UsePhoto(remaining?.StorageKey, remaining?.ContentType);
        await _photos.DeleteAsync(scope.Value.TenantId, _actor.UserId!.Value, photo.StorageKey, cancellationToken).ConfigureAwait(false);
        await Audit("MemberProgressPhotoRemoved", photo.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(photo.Id);
    }

    public async Task<Result<MemberPhotoFile>> Handle(ReadMemberProgressPhotoQuery request, CancellationToken cancellationToken)
    {
        var scope = await Assigned(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<MemberPhotoFile>(scope.Error);
        }

        var rows = await _store.ListProgressAsync(scope.Value.TenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        var assessment = rows.SingleOrDefault(item => item.Id == request.AssessmentId);
        if (assessment is null || !assessment.PhotoConsent)
        {
            return Result.Failure<MemberPhotoFile>(GymErrors.NotFound);
        }

        var key = assessment.PhotoStorageKey;
        var contentType = assessment.PhotoContentType;
        if (request.PhotoId is Guid photoId)
        {
            var photo = (await _store.ListAssessmentPhotosAsync(scope.Value.TenantId, assessment.Id, cancellationToken).ConfigureAwait(false)).SingleOrDefault(item => item.Id == photoId);
            key = photo?.StorageKey;
            contentType = photo?.ContentType;
        }

        if (key is null || contentType is null || !MemberProgressAssessment.OwnsPhoto(scope.Value.TenantId, request.MemberId, assessment.Id, key))
        {
            return Result.Failure<MemberPhotoFile>(GymErrors.NotFound);
        }

        var content = await _photos.OpenAsync(scope.Value.TenantId, key, cancellationToken).ConfigureAwait(false);
        return content.IsFailure
            ? Result.Failure<MemberPhotoFile>(content.Error == StorageErrors.NotFound ? GymErrors.NotFound : content.Error)
            : Result.Success(new MemberPhotoFile(content.Value, contentType));
    }

    private async Task<Result<Guid>> Revise(SaveMemberProgressCommand request, TrainerProgressScope scope, Guid? userId, Guid assessmentId, DateOnly today, CancellationToken cancellationToken)
    {
        var assessment = await _store.GetProgressAsync(scope.TenantId, request.MemberId, assessmentId, cancellationToken).ConfigureAwait(false);
        if (assessment is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        var removedPhotos = assessment.PhotoConsent && !request.PhotoConsent
            ? await _store.ListAssessmentPhotosAsync(scope.TenantId, assessment.Id, cancellationToken).ConfigureAwait(false)
            : [];
        var removedPhoto = assessment.PhotoConsent && !request.PhotoConsent ? assessment.PhotoStorageKey : null;
        if (!assessment.Revise(scope.TrainerId, Capture(request), today))
        {
            return Result.Failure<Guid>(assessment.TrainerProfileId == scope.TrainerId ? GymErrors.InvalidState : IdentityErrors.Forbidden);
        }

        foreach (var photo in removedPhotos)
        {
            photo.SoftDelete(_clock.UtcNow);
        }

        await Mirror(assessment, userId, cancellationToken).ConfigureAwait(false);
        await Audit("MemberProgressRevised", assessment.Id, request.AssessmentDate.ToString("yyyy-MM-dd"), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (removedPhoto is not null && MemberProgressAssessment.OwnsPhoto(scope.TenantId, request.MemberId, assessment.Id, removedPhoto))
        {
            await _photos.DeleteAsync(scope.TenantId, _actor.UserId!.Value, removedPhoto, cancellationToken).ConfigureAwait(false);
        }

        foreach (var photo in removedPhotos)
        {
            if (photo.StorageKey != removedPhoto && MemberProgressAssessment.OwnsPhoto(scope.TenantId, request.MemberId, assessment.Id, photo.StorageKey))
            {
                await _photos.DeleteAsync(scope.TenantId, _actor.UserId!.Value, photo.StorageKey, cancellationToken).ConfigureAwait(false);
            }
        }

        return Result.Success(assessment.Id);
    }

    private async Task Mirror(MemberProgressAssessment assessment, Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not Guid ownerId)
        {
            return;
        }

        if (assessment.BodyMeasurementId is Guid measurementId)
        {
            var existing = await _store.GetMeasurementAsync(assessment.TenantId, measurementId, cancellationToken).ConfigureAwait(false);
            existing?.Apply(assessment.UnitSystem, assessment.AssessmentDate, assessment.Weight, assessment.BodyFatPercentage, assessment.Chest, assessment.Waist, assessment.Hip, assessment.Arm, assessment.Thigh, assessment.MeasurementNotes(), _clock.UtcNow);
            return;
        }

        var measurement = BodyMeasurement.Record(Guid.NewGuid(), ownerId, assessment.TenantId, assessment.UnitSystem, assessment.AssessmentDate, assessment.Weight, assessment.BodyFatPercentage, assessment.Chest, assessment.Waist, assessment.Hip, assessment.Arm, assessment.Thigh, assessment.MeasurementNotes(), _clock.UtcNow);
        assessment.LinkMeasurement(measurement.Id);
        await _store.AddMeasurementAsync(measurement, cancellationToken).ConfigureAwait(false);
    }

    private static ProgressCapture Capture(SaveMemberProgressCommand request) =>
        new(request.AssessmentDate, UnitSystem.Metric, request.Weight, request.Height, request.Waist, request.Chest, request.Hip, request.Arm, request.RightArm, request.Thigh, request.RightThigh, request.BodyFatPercentage, request.VisceralFat, request.FitnessScore, request.StrengthScore, request.EnduranceScore, request.FlexibilityScore, request.CoreScore, request.TrainingPerformance, request.Notes, request.AchievedGoals, request.NextGoals, request.PhotoConsent);

    private async Task<Result<TrainerProgressScope>> Assigned(Guid memberId, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<TrainerProgressScope>(IdentityErrors.Forbidden);
        }

        var trainer = await _store.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (trainer is null || !await _store.IsAssignedAsync(tenantId, trainer.Id, memberId, DateOnly.FromDateTime(_clock.UtcNow), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TrainerProgressScope>(trainer is null ? IdentityErrors.Forbidden : GymErrors.NotFound);
        }

        return Result.Success(new TrainerProgressScope(tenantId, trainer.Id));
    }

    private Task Audit(string action, Guid id, string? detail, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), _actor.TenantId, _actor.UserId, action, "MemberProgressAssessment", id, null, detail, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);

    private readonly record struct TrainerProgressScope(Guid TenantId, Guid TrainerId);
}
