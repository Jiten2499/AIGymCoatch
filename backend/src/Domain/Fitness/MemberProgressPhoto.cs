using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class MemberProgressPhoto : BaseEntity
{
    public const int MaxCount = 6;

    private MemberProgressPhoto()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid AssessmentId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public static MemberProgressPhoto? Create(Guid id, Guid tenantId, Guid memberId, Guid assessmentId, string key, string contentType)
    {
        if (!MemberProgressAssessment.OwnsPhoto(tenantId, memberId, assessmentId, key) || contentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            return null;
        }

        return new MemberProgressPhoto
        {
            Id = id,
            TenantId = tenantId,
            AssessmentId = assessmentId,
            StorageKey = key,
            ContentType = contentType,
        };
    }
}
