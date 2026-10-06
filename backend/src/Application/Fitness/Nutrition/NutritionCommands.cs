using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Nutrition;

public sealed record GetNutritionProfileQuery : IRequest<Result<NutritionProfileResponse>>;

public sealed record SaveNutritionProfileCommand(decimal DailyCalorieTarget, decimal ProteinTarget, decimal CarbTarget, decimal FatTarget, DietaryPreference DietaryPreference, int MealFrequency) : IRequest<Result<NutritionProfileResponse>>;

public sealed class SaveNutritionProfileCommandValidator : AbstractValidator<SaveNutritionProfileCommand>
{
    public SaveNutritionProfileCommandValidator()
    {
        RuleFor(command => command.DailyCalorieTarget).InclusiveBetween(800, 6000);
        RuleFor(command => command.ProteinTarget).GreaterThanOrEqualTo(0);
        RuleFor(command => command.CarbTarget).GreaterThanOrEqualTo(0);
        RuleFor(command => command.FatTarget).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MealFrequency).InclusiveBetween(1, 8);
    }
}

public sealed class NutritionProfileHandler :
    IRequestHandler<GetNutritionProfileQuery, Result<NutritionProfileResponse>>,
    IRequestHandler<SaveNutritionProfileCommand, Result<NutritionProfileResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly INutritionRepository _nutrition;
    private readonly IUnitOfWork _unitOfWork;

    public NutritionProfileHandler(IOperationGuard guard, ICurrentActor actor, INutritionRepository nutrition, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _nutrition = nutrition;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NutritionProfileResponse>> Handle(GetNutritionProfileQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.NutritionRead, FeatureCodes.Nutrition, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<NutritionProfileResponse>(scope.Error);
        }

        var profile = await _nutrition.GetProfileAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        return profile is null ? Result.Failure<NutritionProfileResponse>(FitnessErrors.NotFound) : Result.Success(Map(profile));
    }

    public async Task<Result<NutritionProfileResponse>> Handle(SaveNutritionProfileCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.NutritionManage, FeatureCodes.Nutrition, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<NutritionProfileResponse>(scope.Error);
        }

        var profile = await _nutrition.GetProfileAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            profile = NutritionProfile.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId);
            await _nutrition.AddProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        profile.Update(request.DailyCalorieTarget, request.ProteinTarget, request.CarbTarget, request.FatTarget, request.DietaryPreference, request.MealFrequency);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(profile));
    }

    private static NutritionProfileResponse Map(NutritionProfile profile) =>
        new(profile.Id, profile.DailyCalorieTarget, profile.ProteinTarget, profile.CarbTarget, profile.FatTarget, profile.DietaryPreference, profile.MealFrequency);
}

public sealed record ListNutritionLogsQuery(DateOnly? Date, int Page, int PageSize) : IRequest<Result<PageResult<NutritionLogResponse>>>;

public sealed record CreateNutritionLogCommand(DateOnly LogDate, MealType MealType, string FoodName, decimal Quantity, string Unit, decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat) : IRequest<Result<NutritionLogResponse>>;

public sealed class CreateNutritionLogCommandValidator : AbstractValidator<CreateNutritionLogCommand>
{
    public CreateNutritionLogCommandValidator()
    {
        RuleFor(command => command.FoodName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Unit).NotEmpty().MaximumLength(32);
        RuleFor(command => command.Quantity).GreaterThan(0);
        RuleFor(command => command.Calories).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Protein).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Carbohydrates).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Fat).GreaterThanOrEqualTo(0);
    }
}

public sealed record GetNutritionSummaryQuery(DateOnly Date) : IRequest<Result<NutritionSummaryResponse>>;

public sealed class NutritionLogHandler :
    IRequestHandler<ListNutritionLogsQuery, Result<PageResult<NutritionLogResponse>>>,
    IRequestHandler<CreateNutritionLogCommand, Result<NutritionLogResponse>>,
    IRequestHandler<GetNutritionSummaryQuery, Result<NutritionSummaryResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly INutritionRepository _nutrition;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public NutritionLogHandler(IOperationGuard guard, ICurrentActor actor, INutritionRepository nutrition, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _nutrition = nutrition;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<PageResult<NutritionLogResponse>>> Handle(ListNutritionLogsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.NutritionRead, FeatureCodes.Nutrition, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<NutritionLogResponse>>(scope.Error);
        }

        var page = await _nutrition.ListLogsAsync(scope.Value.TenantId, scope.Value.UserId, request.Date, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<NutritionLogResponse>(page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }

    public async Task<Result<NutritionLogResponse>> Handle(CreateNutritionLogCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.NutritionManage, FeatureCodes.Nutrition, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<NutritionLogResponse>(scope.Error);
        }

        var log = NutritionLog.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, request.LogDate, request.MealType, request.FoodName, request.Quantity, request.Unit, request.Calories, request.Protein, request.Carbohydrates, request.Fat, _clock.UtcNow);
        await _nutrition.AddLogAsync(log, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(log));
    }

    public async Task<Result<NutritionSummaryResponse>> Handle(GetNutritionSummaryQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.NutritionRead, FeatureCodes.Nutrition, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<NutritionSummaryResponse>(scope.Error);
        }

        return Result.Success(await _nutrition.SummarizeDayAsync(scope.Value.TenantId, scope.Value.UserId, request.Date, cancellationToken).ConfigureAwait(false));
    }

    private static NutritionLogResponse Map(NutritionLog log) =>
        new(log.Id, log.LogDate, log.MealType, log.FoodName, log.Quantity, log.Unit, log.Calories, log.Protein, log.Carbohydrates, log.Fat);
}
