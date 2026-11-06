using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Expenses;

public sealed record ExpenseCategoryItem(Guid Id, string Name, string? Description, bool IsActive);

public sealed record ExpenseListItem(
    Guid Id,
    string Category,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly ExpenseDate,
    ExpensePaymentMethod PaymentMethod,
    string? RecordedBy,
    ExpenseStatus Status);

public sealed record ExpenseDetails(
    Guid Id,
    Guid CategoryId,
    string Category,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly ExpenseDate,
    ExpensePaymentMethod PaymentMethod,
    Guid RecordedByUserId,
    string? RecordedBy,
    string? Notes,
    ExpenseStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CategoryTotal(Guid CategoryId, string Name, decimal Amount, int Count);

public sealed record ExpenseSummary(
    decimal TotalAmount,
    int TotalCount,
    decimal TodayAmount,
    int TodayCount,
    decimal WeekAmount,
    decimal MonthAmount,
    string Currency,
    IReadOnlyList<CategoryTotal> Categories);

public sealed record ExpenseSearch(
    string? Term,
    Guid? CategoryId,
    ExpenseStatus? Status,
    ExpensePaymentMethod? PaymentMethod,
    Guid? RecordedByUserId,
    DateOnly? From,
    DateOnly? To,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? Sort,
    int Page,
    int PageSize);
