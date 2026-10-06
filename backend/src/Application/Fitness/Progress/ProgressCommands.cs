using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Progress;

public sealed record ListMeasurementsQuery(int Page, int PageSize) : IRequest<Result<PageResult<MeasurementResponse>>>;

public sealed class ListMeasurementsQueryHandler : IRequestHandler<ListMeasurementsQuery, Result<PageResult<MeasurementResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;

    public ListMeasurementsQueryHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
    }

    public async Task<Result<PageResult<MeasurementResponse>>> Handle(ListMeasurementsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<MeasurementResponse>>(scope.Error);
        }

        var page = await _fitness.ListMeasurementsAsync(scope.Value.TenantId, scope.Value.UserId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<MeasurementResponse>(page.Items.Select(FitnessMaps.ToResponse).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
}

public sealed record RecordMeasurementCommand(UnitSystem UnitSystem, DateOnly MeasurementDate, decimal? Weight, decimal? BodyFatPercentage, decimal? Chest, decimal? Waist, decimal? Hip, decimal? Arm, decimal? Thigh, string? Notes) : IRequest<Result<MeasurementResponse>>;

public sealed class RecordMeasurementCommandValidator : AbstractValidator<RecordMeasurementCommand>
{
    public RecordMeasurementCommandValidator()
    {
        RuleFor(command => command.Weight).InclusiveBetween(1, 500).When(command => command.Weight.HasValue);
        RuleFor(command => command.BodyFatPercentage).InclusiveBetween(0, 100).When(command => command.BodyFatPercentage.HasValue);
        RuleFor(command => command.Notes).MaximumLength(500);
    }
}

public sealed class RecordMeasurementCommandHandler : IRequestHandler<RecordMeasurementCommand, Result<MeasurementResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public RecordMeasurementCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<MeasurementResponse>> Handle(RecordMeasurementCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<MeasurementResponse>(scope.Error);
        }

        var row = BodyMeasurement.Record(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, request.UnitSystem, request.MeasurementDate, request.Weight, request.BodyFatPercentage, request.Chest, request.Waist, request.Hip, request.Arm, request.Thigh, request.Notes, _clock.UtcNow);
        await _fitness.AddMeasurementAsync(row, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(FitnessMaps.ToResponse(row));
    }
}

public sealed record RecordBodyScanCommand(DateOnly ScanDate, BodyScanSource Source, string StorageReference) : IRequest<Result<Guid>>;

public sealed class RecordBodyScanCommandHandler : IRequestHandler<RecordBodyScanCommand, Result<Guid>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public RecordBodyScanCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(RecordBodyScanCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<Guid>(scope.Error);
        }

        if (!BodyScanRecord.IsSafeReference(request.StorageReference))
        {
            return Result.Failure<Guid>(FitnessErrors.InvalidReference);
        }

        var scan = BodyScanRecord.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, request.ScanDate, request.Source, request.StorageReference, _clock.UtcNow);
        await _fitness.AddScanAsync(scan, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(scan.Id);
    }
}

public sealed record GetProgressQuery : IRequest<Result<DashboardResponse>>;

public sealed class GetProgressQueryHandler : IRequestHandler<GetProgressQuery, Result<DashboardResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMemberDashboardQuery _dashboard;
    private readonly IDateTimeProvider _clock;

    public GetProgressQueryHandler(IOperationGuard guard, ICurrentActor actor, IMemberDashboardQuery dashboard, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _dashboard = dashboard;
        _clock = clock;
    }

    public async Task<Result<DashboardResponse>> Handle(GetProgressQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<DashboardResponse>(scope.Error);
        }

        return Result.Success(await _dashboard.GetAsync(scope.Value.TenantId, scope.Value.UserId, DateOnly.FromDateTime(_clock.UtcNow), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record GetMemberDashboardQuery : IRequest<Result<DashboardResponse>>;

public sealed class GetMemberDashboardQueryHandler : IRequestHandler<GetMemberDashboardQuery, Result<DashboardResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMemberDashboardQuery _dashboard;
    private readonly IDateTimeProvider _clock;

    public GetMemberDashboardQueryHandler(IOperationGuard guard, ICurrentActor actor, IMemberDashboardQuery dashboard, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _dashboard = dashboard;
        _clock = clock;
    }

    public async Task<Result<DashboardResponse>> Handle(GetMemberDashboardQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<DashboardResponse>(scope.Error);
        }

        return Result.Success(await _dashboard.GetAsync(scope.Value.TenantId, scope.Value.UserId, DateOnly.FromDateTime(_clock.UtcNow), cancellationToken).ConfigureAwait(false));
    }
}
