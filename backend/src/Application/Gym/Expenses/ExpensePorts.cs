using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Expenses;

public interface IExpenseRepository
{
    Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<ExpenseCategory?> GetCategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> CategoryNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task AddCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseCategoryItem>> ListCategoriesAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken);

    Task<Expense?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Expense expense, CancellationToken cancellationToken);

    Task<PageResult<ExpenseListItem>> SearchAsync(Guid tenantId, ExpenseSearch search, CancellationToken cancellationToken);

    Task<ExpenseDetails?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<ExpenseSummary> SummarizeAsync(Guid tenantId, DateOnly today, DateOnly? from, DateOnly? to, string currency, CancellationToken cancellationToken);
}
