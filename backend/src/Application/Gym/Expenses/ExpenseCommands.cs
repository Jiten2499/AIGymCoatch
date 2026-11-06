using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Expenses;

public sealed record CreateExpenseCommand(Guid CategoryId, string Description, decimal Amount, DateOnly ExpenseDate, ExpensePaymentMethod PaymentMethod, string? Notes) : IRequest<Result<ExpenseDetails>>;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(command => command.Description).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Amount).Must(ExpenseRules.Amount);
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

public sealed class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Result<ExpenseDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly ILedgerPostingService _ledger;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateExpenseCommandHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, ILedgerPostingService ledger, IBusinessProfileRepository profiles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _ledger = ledger;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<ExpenseDetails>> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseCreate, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<ExpenseDetails>(allowed.Error);
        }

        var category = await _expenses.GetCategoryAsync(tenantId, request.CategoryId, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return Result.Failure<ExpenseDetails>(GymErrors.NotFound);
        }

        if (!category.IsActive)
        {
            return Result.Failure<ExpenseDetails>(GymErrors.InvalidState);
        }

        var today = await TodayAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!ExpenseRules.Date(request.ExpenseDate, today))
        {
            return Result.Failure<ExpenseDetails>(GymErrors.InvalidState);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var expense = Expense.Create(Guid.NewGuid(), tenantId, category.Id, request.Description, request.Amount, profile?.Currency ?? "USD", request.ExpenseDate, request.PaymentMethod, userId, request.Notes);
        await _expenses.AddAsync(expense, cancellationToken).ConfigureAwait(false);
        var ledger = await _ledger.TryPostExpenseAsync(tenantId, expense, category.Name, userId, cancellationToken).ConfigureAwait(false);
        if (ledger.IsFailure)
        {
            return Result.Failure<ExpenseDetails>(ledger.Error);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "ExpenseCreated", "Expense", expense.Id, null, expense.Amount.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _expenses.ReadAsync(tenantId, expense.Id, cancellationToken).ConfigureAwait(false))!);
    }

    private async Task<DateOnly> TodayAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return ExpenseCalendar.LocalDate(_clock.UtcNow, ExpenseCalendar.Zone(profile?.TimeZone));
    }
}

public sealed record UpdateExpenseCommand(Guid Id, Guid CategoryId, string Description, decimal Amount, DateOnly ExpenseDate, ExpensePaymentMethod PaymentMethod, string? Notes) : IRequest<Result<ExpenseDetails>>;

public sealed class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(command => command.Description).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Amount).Must(ExpenseRules.Amount);
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, Result<ExpenseDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdateExpenseCommandHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, IBusinessProfileRepository profiles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<ExpenseDetails>> Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseUpdate, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<ExpenseDetails>(allowed.Error);
        }

        var expense = await _expenses.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var category = expense is null ? null : await _expenses.GetCategoryAsync(tenantId, request.CategoryId, cancellationToken).ConfigureAwait(false);
        if (expense is null || category is null)
        {
            return Result.Failure<ExpenseDetails>(GymErrors.NotFound);
        }

        if (!category.IsActive && category.Id != expense.CategoryId)
        {
            return Result.Failure<ExpenseDetails>(GymErrors.InvalidState);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var today = ExpenseCalendar.LocalDate(_clock.UtcNow, ExpenseCalendar.Zone(profile?.TimeZone));
        var previous = expense.Amount;
        if (!ExpenseRules.Date(request.ExpenseDate, today) || !expense.Update(category.Id, request.Description, request.Amount, request.ExpenseDate, request.PaymentMethod, request.Notes))
        {
            return Result.Failure<ExpenseDetails>(GymErrors.InvalidState);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "ExpenseUpdated", "Expense", expense.Id, previous.ToString(), expense.Amount.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _expenses.ReadAsync(tenantId, expense.Id, cancellationToken).ConfigureAwait(false))!);
    }
}

public sealed record VoidExpenseCommand(Guid Id) : IRequest<Result<ExpenseDetails>>;

public sealed class VoidExpenseCommandHandler : IRequestHandler<VoidExpenseCommand, Result<ExpenseDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly ILedgerPostingService _ledger;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public VoidExpenseCommandHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, ILedgerPostingService ledger, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _ledger = ledger;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<ExpenseDetails>> Handle(VoidExpenseCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseVoid, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<ExpenseDetails>(allowed.Error);
        }

        var expense = await _expenses.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (expense is null || !expense.Void(userId, _clock.UtcNow))
        {
            return Result.Failure<ExpenseDetails>(expense is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        var ledger = await _ledger.TryReverseExpenseAsync(tenantId, expense, userId, cancellationToken).ConfigureAwait(false);
        if (ledger.IsFailure)
        {
            return Result.Failure<ExpenseDetails>(ledger.Error);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "ExpenseVoided", "Expense", expense.Id, ExpenseStatus.Active.ToString(), expense.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _expenses.ReadAsync(tenantId, expense.Id, cancellationToken).ConfigureAwait(false))!);
    }
}
