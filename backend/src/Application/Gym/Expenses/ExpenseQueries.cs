using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Expenses;

public sealed record GetExpenseCategoriesQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<ExpenseCategoryItem>>>;

public sealed class GetExpenseCategoriesQueryHandler : IRequestHandler<GetExpenseCategoriesQuery, Result<IReadOnlyList<ExpenseCategoryItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly IUnitOfWork _unitOfWork;

    public GetExpenseCategoriesQueryHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<ExpenseCategoryItem>>> Handle(GetExpenseCategoriesQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseRead, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<ExpenseCategoryItem>>(allowed.Error);
        }

        await _expenses.EnsureDefaultsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await _expenses.ListCategoriesAsync(tenantId, request.ActiveOnly, cancellationToken).ConfigureAwait(false));
    }
}

public sealed record GetExpenseQuery(Guid Id) : IRequest<Result<ExpenseDetails>>;

public sealed class GetExpenseQueryHandler : IRequestHandler<GetExpenseQuery, Result<ExpenseDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;

    public GetExpenseQueryHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
    }

    public async Task<Result<ExpenseDetails>> Handle(GetExpenseQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseRead, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<ExpenseDetails>(allowed.Error);
        }

        var expense = await _expenses.ReadAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return expense is null ? Result.Failure<ExpenseDetails>(GymErrors.NotFound) : Result.Success(expense);
    }
}

public sealed record GetExpensesQuery(string? Term, Guid? CategoryId, ExpenseStatus? Status, ExpensePaymentMethod? PaymentMethod, Guid? RecordedByUserId, DateOnly? From, DateOnly? To, decimal? MinAmount, decimal? MaxAmount, int Page, int PageSize, string? Sort) : IRequest<Result<PageResult<ExpenseListItem>>>;

public sealed class GetExpensesQueryHandler : IRequestHandler<GetExpensesQuery, Result<PageResult<ExpenseListItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;

    public GetExpensesQueryHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
    }

    public async Task<Result<PageResult<ExpenseListItem>>> Handle(GetExpensesQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseRead, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<ExpenseListItem>>(allowed.Error);
        }

        var page = await _expenses.SearchAsync(tenantId, new ExpenseSearch(request.Term, request.CategoryId, request.Status, request.PaymentMethod, request.RecordedByUserId, request.From, request.To, request.MinAmount, request.MaxAmount, request.Sort, request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }
}

public sealed record GetExpenseSummaryQuery(DateOnly? From, DateOnly? To) : IRequest<Result<ExpenseSummary>>;

public sealed class GetExpenseSummaryQueryHandler : IRequestHandler<GetExpenseSummaryQuery, Result<ExpenseSummary>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IDateTimeProvider _clock;

    public GetExpenseSummaryQueryHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, IBusinessProfileRepository profiles, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result<ExpenseSummary>> Handle(GetExpenseSummaryQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseRead, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<ExpenseSummary>(allowed.Error);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var today = ExpenseCalendar.LocalDate(_clock.UtcNow, ExpenseCalendar.Zone(profile?.TimeZone));
        var summary = await _expenses.SummarizeAsync(tenantId, today, request.From, request.To, profile?.Currency ?? "USD", cancellationToken).ConfigureAwait(false);
        return Result.Success(summary);
    }
}
