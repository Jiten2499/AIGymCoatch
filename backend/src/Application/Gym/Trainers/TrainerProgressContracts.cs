using FluentValidation;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record ProgressValue(decimal? Value, decimal? Previous, decimal? Change);

public sealed record MemberProgressItem(
    Guid Id,
    DateOnly AssessmentDate,
    string UnitSystem,
    ProgressValue Weight,
    ProgressValue Waist,
    ProgressValue Chest,
    ProgressValue Hip,
    ProgressValue Arm,
    ProgressValue Thigh,
    ProgressValue BodyFatPercentage,
    ProgressValue FitnessScore,
    string? TrainingPerformance,
    string? Notes,
    string? AchievedGoals,
    string? NextGoals,
    bool PhotoConsent,
    bool HasPhoto,
    bool CanEdit,
    ProgressValue Height,
    ProgressValue RightArm,
    ProgressValue RightThigh,
    ProgressValue VisceralFat,
    ProgressValue StrengthScore,
    ProgressValue EnduranceScore,
    ProgressValue FlexibilityScore,
    ProgressValue CoreScore,
    IReadOnlyList<Guid> PhotoIds);

public sealed record ListMemberProgressQuery(Guid MemberId) : IRequest<Result<IReadOnlyList<MemberProgressItem>>>;

public sealed record SaveMemberProgressCommand(
    Guid MemberId,
    Guid? AssessmentId,
    DateOnly AssessmentDate,
    decimal? Weight,
    decimal? Waist,
    decimal? Chest,
    decimal? Hip,
    decimal? Arm,
    decimal? Thigh,
    decimal? BodyFatPercentage,
    decimal? FitnessScore,
    string? TrainingPerformance,
    string? Notes,
    string? AchievedGoals,
    string? NextGoals,
    bool PhotoConsent,
    decimal? Height = null,
    decimal? RightArm = null,
    decimal? RightThigh = null,
    decimal? VisceralFat = null,
    decimal? StrengthScore = null,
    decimal? EnduranceScore = null,
    decimal? FlexibilityScore = null,
    decimal? CoreScore = null) : IRequest<Result<Guid>>;

public sealed class SaveMemberProgressCommandValidator : AbstractValidator<SaveMemberProgressCommand>
{
    public SaveMemberProgressCommandValidator()
    {
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.AssessmentDate).NotEmpty();
        RuleFor(command => command.Weight).Must(Measure);
        RuleFor(command => command.Waist).Must(Measure);
        RuleFor(command => command.Chest).Must(Measure);
        RuleFor(command => command.Hip).Must(Measure);
        RuleFor(command => command.Arm).Must(Measure);
        RuleFor(command => command.Thigh).Must(Measure);
        RuleFor(command => command.BodyFatPercentage).Must(Percent);
        RuleFor(command => command.FitnessScore).Must(Percent);
        RuleFor(command => command.Height).Must(Measure);
        RuleFor(command => command.RightArm).Must(Measure);
        RuleFor(command => command.RightThigh).Must(Measure);
        RuleFor(command => command.VisceralFat).Must(value => value is null || value is > 0 and <= 60);
        RuleFor(command => command.StrengthScore).Must(Score);
        RuleFor(command => command.EnduranceScore).Must(Score);
        RuleFor(command => command.FlexibilityScore).Must(Score);
        RuleFor(command => command.CoreScore).Must(Score);
        RuleFor(command => command.TrainingPerformance).MaximumLength(500);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.AchievedGoals).MaximumLength(500);
        RuleFor(command => command.NextGoals).MaximumLength(500);
    }

    private static bool Measure(decimal? value) => value is null || value is > 0 and <= 500;

    private static bool Percent(decimal? value) => value is null || value is >= 0 and <= 100;

    private static bool Score(decimal? value) => value is null || value is >= 1 and <= 10;
}

public sealed record DeleteMemberProgressPhotoCommand(Guid MemberId, Guid AssessmentId, Guid PhotoId) : IRequest<Result<Guid>>;

public sealed record UploadMemberProgressPhotoCommand(Guid MemberId, Guid AssessmentId, string? ContentType, byte[] Content) : IRequest<Result<Guid>>;

public sealed record ReadMemberProgressPhotoQuery(Guid MemberId, Guid AssessmentId, Guid? PhotoId = null) : IRequest<Result<MemberPhotoFile>>;
