using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class MemberProgressAssessment : BaseEntity
{
    private MemberProgressAssessment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public Guid? BodyMeasurementId { get; private set; }

    public DateOnly AssessmentDate { get; private set; }

    public UnitSystem UnitSystem { get; private set; }

    public decimal? Weight { get; private set; }

    public decimal? Waist { get; private set; }

    public decimal? Chest { get; private set; }

    public decimal? Hip { get; private set; }

    public decimal? Arm { get; private set; }

    public decimal? RightArm { get; private set; }

    public decimal? Thigh { get; private set; }

    public decimal? RightThigh { get; private set; }

    public decimal? Height { get; private set; }

    public decimal? BodyFatPercentage { get; private set; }

    public decimal? VisceralFat { get; private set; }

    public decimal? FitnessScore { get; private set; }

    public decimal? StrengthScore { get; private set; }

    public decimal? EnduranceScore { get; private set; }

    public decimal? FlexibilityScore { get; private set; }

    public decimal? CoreScore { get; private set; }

    public string? TrainingPerformance { get; private set; }

    public string? Notes { get; private set; }

    public string? AchievedGoals { get; private set; }

    public string? NextGoals { get; private set; }

    public bool PhotoConsent { get; private set; }

    public string? PhotoStorageKey { get; private set; }

    public string? PhotoContentType { get; private set; }

    public static string PhotoKey(Guid tenantId, Guid memberId, Guid assessmentId, Guid photoId) =>
        $"tenants/{tenantId:D}/members/{memberId:D}/progress/{assessmentId:D}/{photoId:D}";

    public static bool OwnsPhoto(Guid tenantId, Guid memberId, Guid assessmentId, string? key) =>
        key is not null && key.StartsWith($"tenants/{tenantId:D}/members/{memberId:D}/progress/{assessmentId:D}/", StringComparison.Ordinal);

    public static MemberProgressAssessment? Record(Guid id, Guid tenantId, Guid memberId, Guid trainerId, ProgressCapture capture, DateOnly today)
    {
        if (!ValuesAreValid(capture, today))
        {
            return null;
        }

        var assessment = new MemberProgressAssessment { Id = id, TenantId = tenantId, MemberProfileId = memberId, TrainerProfileId = trainerId };
        assessment.Apply(capture);
        return assessment;
    }

    public bool Revise(Guid trainerId, ProgressCapture capture, DateOnly today)
    {
        if (TrainerProfileId != trainerId || !ValuesAreValid(capture, today))
        {
            return false;
        }

        Apply(capture);
        if (!capture.PhotoConsent)
        {
            PhotoStorageKey = null;
            PhotoContentType = null;
        }

        return true;
    }

    public void UsePhoto(string? key, string? contentType)
    {
        PhotoStorageKey = key;
        PhotoContentType = contentType;
    }

    public void LinkMeasurement(Guid measurementId) => BodyMeasurementId = measurementId;

    public bool AttachPhoto(Guid trainerId, string key, string contentType)
    {
        if (TrainerProfileId != trainerId || !PhotoConsent || !OwnsPhoto(TenantId, MemberProfileId, Id, key))
        {
            return false;
        }

        PhotoStorageKey = key;
        PhotoContentType = contentType;
        return true;
    }

    public string? MeasurementNotes() => Notes is { Length: > 500 } ? Notes[..500] : Notes;

    private void Apply(ProgressCapture capture)
    {
        AssessmentDate = capture.Date;
        UnitSystem = capture.UnitSystem;
        Weight = capture.Weight;
        Height = capture.Height;
        Waist = capture.Waist;
        Chest = capture.Chest;
        Hip = capture.Hip;
        Arm = capture.Arm;
        RightArm = capture.RightArm;
        Thigh = capture.Thigh;
        RightThigh = capture.RightThigh;
        BodyFatPercentage = capture.BodyFatPercentage;
        VisceralFat = capture.VisceralFat;
        FitnessScore = capture.FitnessScore;
        StrengthScore = capture.StrengthScore;
        EnduranceScore = capture.EnduranceScore;
        FlexibilityScore = capture.FlexibilityScore;
        CoreScore = capture.CoreScore;
        TrainingPerformance = Clean(capture.TrainingPerformance, 500);
        Notes = Clean(capture.Notes, 1000);
        AchievedGoals = Clean(capture.AchievedGoals, 500);
        NextGoals = Clean(capture.NextGoals, 500);
        PhotoConsent = capture.PhotoConsent;
    }

    private static bool ValuesAreValid(ProgressCapture capture, DateOnly today) =>
        capture.Date != default && capture.Date <= today
        && Measure(capture.Weight) && Measure(capture.Height) && Measure(capture.Waist) && Measure(capture.Chest) && Measure(capture.Hip)
        && Measure(capture.Arm) && Measure(capture.RightArm) && Measure(capture.Thigh) && Measure(capture.RightThigh)
        && Percent(capture.BodyFatPercentage) && Percent(capture.FitnessScore) && Level(capture.VisceralFat)
        && Score(capture.StrengthScore) && Score(capture.EnduranceScore) && Score(capture.FlexibilityScore) && Score(capture.CoreScore)
        && Fits(capture.TrainingPerformance, 500) && Fits(capture.Notes, 1000) && Fits(capture.AchievedGoals, 500) && Fits(capture.NextGoals, 500);

    private static bool Measure(decimal? value) => value is null || value is > 0 and <= 500;

    private static bool Percent(decimal? value) => value is null || value is >= 0 and <= 100;

    private static bool Level(decimal? value) => value is null || value is > 0 and <= 60;

    private static bool Score(decimal? value) => value is null || value is >= 1 and <= 10;

    private static bool Fits(string? value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= max;

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : null;
    }
}
