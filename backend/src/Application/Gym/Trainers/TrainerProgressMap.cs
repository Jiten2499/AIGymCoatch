using LifeFitAI.Domain.Fitness;

namespace LifeFitAI.Application.Gym.Trainers;

public static class TrainerProgressMap
{
    public static IReadOnlyList<MemberProgressItem> Items(IReadOnlyList<MemberProgressAssessment> rows, IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> photos, Guid trainerId)
    {
        MemberProgressAssessment? previous = null;
        var mapped = new List<MemberProgressItem>(rows.Count);
        foreach (var row in rows)
        {
            var photoIds = photos.TryGetValue(row.Id, out var ids) ? ids : [];
            mapped.Add(new MemberProgressItem(
                row.Id,
                row.AssessmentDate,
                row.UnitSystem.ToString(),
                Value(row.Weight, previous?.Weight),
                Value(row.Waist, previous?.Waist),
                Value(row.Chest, previous?.Chest),
                Value(row.Hip, previous?.Hip),
                Value(row.Arm, previous?.Arm),
                Value(row.Thigh, previous?.Thigh),
                Value(row.BodyFatPercentage, previous?.BodyFatPercentage),
                Value(row.FitnessScore, previous?.FitnessScore),
                row.TrainingPerformance,
                row.Notes,
                row.AchievedGoals,
                row.NextGoals,
                row.PhotoConsent,
                photoIds.Count > 0 || row.PhotoStorageKey is not null,
                row.TrainerProfileId == trainerId,
                Value(row.Height, previous?.Height),
                Value(row.RightArm, previous?.RightArm),
                Value(row.RightThigh, previous?.RightThigh),
                Value(row.VisceralFat, previous?.VisceralFat),
                Value(row.StrengthScore, previous?.StrengthScore),
                Value(row.EnduranceScore, previous?.EnduranceScore),
                Value(row.FlexibilityScore, previous?.FlexibilityScore),
                Value(row.CoreScore, previous?.CoreScore),
                photoIds));
            previous = row;
        }

        mapped.Reverse();
        return mapped;
    }

    private static ProgressValue Value(decimal? current, decimal? previous) =>
        new(current, previous, current is decimal left && previous is decimal right ? left - right : null);
}
