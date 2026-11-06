using LifeFitAI.Domain.Fitness;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class TrainerMobileStore
{
    public async Task<IReadOnlyList<MemberProgressAssessment>> ListProgressAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken) =>
        await _db.MemberProgressAssessments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.MemberProfileId == memberId)
            .OrderBy(item => item.AssessmentDate)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<MemberProgressAssessment?> GetProgressAsync(Guid tenantId, Guid memberId, Guid assessmentId, CancellationToken cancellationToken) =>
        _db.MemberProgressAssessments.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.MemberProfileId == memberId && item.Id == assessmentId, cancellationToken);

    public async Task AddProgressAsync(MemberProgressAssessment assessment, CancellationToken cancellationToken) =>
        await _db.MemberProgressAssessments.AddAsync(assessment, cancellationToken).ConfigureAwait(false);

    public Task<BodyMeasurement?> GetMeasurementAsync(Guid tenantId, Guid measurementId, CancellationToken cancellationToken) =>
        _db.BodyMeasurements.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == measurementId, cancellationToken);

    public async Task AddMeasurementAsync(BodyMeasurement measurement, CancellationToken cancellationToken) =>
        await _db.BodyMeasurements.AddAsync(measurement, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MemberProgressPhoto>> ListProgressPhotosAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken) =>
        await _db.MemberProgressPhotos.AsNoTracking()
            .Where(photo => photo.TenantId == tenantId && _db.MemberProgressAssessments.Any(item => item.Id == photo.AssessmentId && item.TenantId == tenantId && item.MemberProfileId == memberId))
            .OrderBy(photo => photo.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<List<MemberProgressPhoto>> ListAssessmentPhotosAsync(Guid tenantId, Guid assessmentId, CancellationToken cancellationToken) =>
        _db.MemberProgressPhotos.Where(photo => photo.TenantId == tenantId && photo.AssessmentId == assessmentId).OrderBy(photo => photo.CreatedAt).ToListAsync(cancellationToken);

    public async Task AddProgressPhotoAsync(MemberProgressPhoto photo, CancellationToken cancellationToken) =>
        await _db.MemberProgressPhotos.AddAsync(photo, cancellationToken).ConfigureAwait(false);
}
