using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Expenses;

public sealed record CreateExpenseCategoryCommand(string Name, string? Description) : IRequest<Result<ExpenseCategoryItem>>;

public sealed class CreateExpenseCategoryCommandValidator : AbstractValidator<CreateExpenseCategoryCommand>
{
    public CreateExpenseCategoryCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

public sealed class CreateExpenseCategoryCommandHandler : IRequestHandler<CreateExpenseCategoryCommand, Result<ExpenseCategoryItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateExpenseCategoryCommandHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<ExpenseCategoryItem>> Handle(CreateExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseCategoryManage, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<ExpenseCategoryItem>(allowed.Error);
        }

        if (await _expenses.CategoryNameExistsAsync(tenantId, request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ExpenseCategoryItem>(GymErrors.Conflict);
        }

        var category = ExpenseCategory.Create(Guid.NewGuid(), tenantId, request.Name, request.Description);
        await _expenses.AddCategoryAsync(category, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "ExpenseCategoryCreated", "ExpenseCategory", category.Id, null, category.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ExpenseCategoryItem(category.Id, category.Name, category.Description, category.IsActive));
    }
}

public sealed record UpdateExpenseCategoryCommand(Guid Id, string Name, string? Description) : IRequest<Result<ExpenseCategoryItem>>;

public sealed class UpdateExpenseCategoryCommandValidator : AbstractValidator<UpdateExpenseCategoryCommand>
{
    public UpdateExpenseCategoryCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

public sealed class UpdateExpenseCategoryCommandHandler : IRequestHandler<UpdateExpenseCategoryCommand, Result<ExpenseCategoryItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdateExpenseCategoryCommandHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<ExpenseCategoryItem>> Handle(UpdateExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseCategoryManage, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<ExpenseCategoryItem>(allowed.Error);
        }

        var category = await _expenses.GetCategoryAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return Result.Failure<ExpenseCategoryItem>(GymErrors.NotFound);
        }

        if (await _expenses.CategoryNameExistsAsync(tenantId, request.Name, category.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ExpenseCategoryItem>(GymErrors.Conflict);
        }

        category.Update(request.Name, request.Description);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "ExpenseCategoryUpdated", "ExpenseCategory", category.Id, null, category.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ExpenseCategoryItem(category.Id, category.Name, category.Description, category.IsActive));
    }
}

public sealed record ChangeExpenseCategoryStatusCommand(Guid Id, bool Active) : IRequest<Result<ExpenseCategoryItem>>;

public sealed class ChangeExpenseCategoryStatusCommandHandler : IRequestHandler<ChangeExpenseCategoryStatusCommand, Result<ExpenseCategoryItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IExpenseRepository _expenses;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ChangeExpenseCategoryStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, IExpenseRepository expenses, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _expenses = expenses;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<ExpenseCategoryItem>> Handle(ChangeExpenseCategoryStatusCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ExpenseCategoryManage, FeatureCodes.GymExpenseManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<ExpenseCategoryItem>(allowed.Error);
        }

        var category = await _expenses.GetCategoryAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return Result.Failure<ExpenseCategoryItem>(GymErrors.NotFound);
        }

        if (request.Active)
        {
            category.Activate();
        }
        else
        {
            category.Deactivate();
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, request.Active ? "ExpenseCategoryUpdated" : "ExpenseCategoryDeactivated", "ExpenseCategory", category.Id, null, category.IsActive.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ExpenseCategoryItem(category.Id, category.Name, category.Description, category.IsActive));
    }
}
