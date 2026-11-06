using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class TrainerRewardSettings : BaseEntity
{
    private TrainerRewardSettings()
    {
    }

    public Guid TenantId { get; private set; }

    public bool PointsEnabled { get; private set; }

    public int DefaultAwardPoints { get; private set; }

    public bool RequireApprovalForAwards { get; private set; }

    public bool AllowManualAdjustments { get; private set; }

    public static TrainerRewardSettings CreateDefault(Guid id, Guid tenantId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            PointsEnabled = true,
            DefaultAwardPoints = 10,
            RequireApprovalForAwards = true,
            AllowManualAdjustments = true,
        };

    public void Update(bool pointsEnabled, int defaultAwardPoints, bool requireApprovalForAwards, bool allowManualAdjustments)
    {
        PointsEnabled = pointsEnabled;
        DefaultAwardPoints = Math.Max(0, defaultAwardPoints);
        RequireApprovalForAwards = requireApprovalForAwards;
        AllowManualAdjustments = allowManualAdjustments;
    }
}

public static class TrainerRewardBalance
{
    public static int From(IEnumerable<TrainerRewardTransaction> rows) =>
        rows.Where(row => row.ApprovalStatus == TrainerRewardApprovalStatus.Approved).Sum(row => row.Points);
}
