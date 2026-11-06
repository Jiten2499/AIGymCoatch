using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly ApplicationDbContext _db;

    public ExpenseRepository(ApplicationDbContext db) => _db = db;

    public async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (await _db.ExpenseCategories.AnyAsync(category => category.TenantId == tenantId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (var name in ExpenseRules.DefaultCategories)
        {
            await _db.ExpenseCategories.AddAsync(ExpenseCategory.Create(Guid.NewGuid(), tenantId, name, null), cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<ExpenseCategory?> GetCategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.ExpenseCategories.SingleOrDefaultAsync(category => category.TenantId == tenantId && category.Id == id, cancellationToken);

    public Task<bool> CategoryNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var value = name.Trim().ToLower();
        return _db.ExpenseCategories.AnyAsync(category => category.TenantId == tenantId && category.Id != excludeId && category.Name.ToLower() == value, cancellationToken);
    }

    public async Task AddCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken) =>
        await _db.ExpenseCategories.AddAsync(category, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ExpenseCategoryItem>> ListCategoriesAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _db.ExpenseCategories.AsNoTracking().Where(category => category.TenantId == tenantId);
        if (activeOnly)
        {
            query = query.Where(category => category.IsActive);
        }

        return await query.OrderBy(category => category.Name)
            .Select(category => new ExpenseCategoryItem(category.Id, category.Name, category.Description, category.IsActive))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<Expense?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.Expenses.SingleOrDefaultAsync(expense => expense.TenantId == tenantId && expense.Id == id, cancellationToken);

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken) =>
        await _db.Expenses.AddAsync(expense, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<ExpenseListItem>> SearchAsync(Guid tenantId, ExpenseSearch search, CancellationToken cancellationToken)
    {
        var query = Filter(_db.Expenses.AsNoTracking().Where(expense => expense.TenantId == tenantId), search);
        query = search.Sort switch
        {
            "amount" => query.OrderByDescending(expense => expense.Amount),
            "description" => query.OrderBy(expense => expense.Description),
            _ => query.OrderByDescending(expense => expense.ExpenseDate),
        };
        var page = new PageQuery(search.Page, search.PageSize);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = await NamesAsync(rows.Select(expense => expense.CategoryId), rows.Select(expense => expense.RecordedByUserId), cancellationToken).ConfigureAwait(false);
        var items = rows.Select(expense => new ExpenseListItem(
            expense.Id,
            names.Categories.GetValueOrDefault(expense.CategoryId) ?? string.Empty,
            expense.Description,
            expense.Amount,
            expense.Currency,
            expense.ExpenseDate,
            expense.PaymentMethod,
            names.People.GetValueOrDefault(expense.RecordedByUserId),
            expense.Status)).ToArray();
        return new PageResult<ExpenseListItem>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<ExpenseDetails?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var expense = await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken).ConfigureAwait(false);
        if (expense is null)
        {
            return null;
        }

        var names = await NamesAsync([expense.CategoryId], [expense.RecordedByUserId], cancellationToken).ConfigureAwait(false);
        return new ExpenseDetails(
            expense.Id,
            expense.CategoryId,
            names.Categories.GetValueOrDefault(expense.CategoryId) ?? string.Empty,
            expense.Description,
            expense.Amount,
            expense.Currency,
            expense.ExpenseDate,
            expense.PaymentMethod,
            expense.RecordedByUserId,
            names.People.GetValueOrDefault(expense.RecordedByUserId),
            expense.Notes,
            expense.Status,
            expense.CreatedAt,
            expense.UpdatedAt);
    }

    public async Task<ExpenseSummary> SummarizeAsync(Guid tenantId, DateOnly today, DateOnly? from, DateOnly? to, string currency, CancellationToken cancellationToken)
    {
        var active = _db.Expenses.AsNoTracking().Where(expense => expense.TenantId == tenantId && expense.Status == ExpenseStatus.Active);
        if (from is not null)
        {
            active = active.Where(expense => expense.ExpenseDate >= from);
        }

        if (to is not null)
        {
            active = active.Where(expense => expense.ExpenseDate <= to);
        }

        var weekStart = ExpenseCalendar.WeekStart(today);
        var monthStart = ExpenseCalendar.MonthStart(today);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var groups = await active.GroupBy(expense => expense.CategoryId)
            .Select(group => new { CategoryId = group.Key, Amount = group.Sum(expense => expense.Amount), Count = group.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var categoryNames = await NamesAsync(groups.Select(group => group.CategoryId), [], cancellationToken).ConfigureAwait(false);
        var calendar = _db.Expenses.AsNoTracking().Where(expense => expense.TenantId == tenantId && expense.Status == ExpenseStatus.Active);
        return new ExpenseSummary(
            groups.Sum(group => group.Amount),
            groups.Sum(group => group.Count),
            await calendar.Where(expense => expense.ExpenseDate == today).SumAsync(expense => (decimal?)expense.Amount, cancellationToken).ConfigureAwait(false) ?? 0,
            await calendar.CountAsync(expense => expense.ExpenseDate == today, cancellationToken).ConfigureAwait(false),
            await calendar.Where(expense => expense.ExpenseDate >= weekStart && expense.ExpenseDate <= today).SumAsync(expense => (decimal?)expense.Amount, cancellationToken).ConfigureAwait(false) ?? 0,
            await calendar.Where(expense => expense.ExpenseDate >= monthStart && expense.ExpenseDate <= monthEnd).SumAsync(expense => (decimal?)expense.Amount, cancellationToken).ConfigureAwait(false) ?? 0,
            currency,
            groups.Select(group => new CategoryTotal(group.CategoryId, categoryNames.Categories.GetValueOrDefault(group.CategoryId) ?? string.Empty, group.Amount, group.Count)).ToArray());
    }

    private static IQueryable<Expense> Filter(IQueryable<Expense> query, ExpenseSearch search)
    {
        if (search.CategoryId is Guid categoryId)
        {
            query = query.Where(expense => expense.CategoryId == categoryId);
        }

        if (search.Status is not null)
        {
            query = query.Where(expense => expense.Status == search.Status);
        }

        if (search.PaymentMethod is not null)
        {
            query = query.Where(expense => expense.PaymentMethod == search.PaymentMethod);
        }

        if (search.RecordedByUserId is Guid userId)
        {
            query = query.Where(expense => expense.RecordedByUserId == userId);
        }

        if (search.From is not null)
        {
            query = query.Where(expense => expense.ExpenseDate >= search.From);
        }

        if (search.To is not null)
        {
            query = query.Where(expense => expense.ExpenseDate <= search.To);
        }

        if (search.MinAmount is not null)
        {
            query = query.Where(expense => expense.Amount >= search.MinAmount);
        }

        if (search.MaxAmount is not null)
        {
            query = query.Where(expense => expense.Amount <= search.MaxAmount);
        }

        if (!string.IsNullOrWhiteSpace(search.Term))
        {
            var value = search.Term.Trim();
            query = query.Where(expense => expense.Description.Contains(value));
        }

        return query;
    }

    private async Task<(Dictionary<Guid, string> Categories, Dictionary<Guid, string> People)> NamesAsync(IEnumerable<Guid> categoryIds, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var categories = categoryIds.Distinct().ToArray();
        var people = userIds.Distinct().ToArray();
        var categoryNames = categories.Length == 0
            ? []
            : await _db.ExpenseCategories.AsNoTracking().Where(category => categories.Contains(category.Id)).ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken).ConfigureAwait(false);
        var personNames = people.Length == 0
            ? []
            : await _db.Users.AsNoTracking().Where(user => people.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.FirstName + " " + user.LastName, cancellationToken).ConfigureAwait(false);
        return (categoryNames, personNames);
    }
}
