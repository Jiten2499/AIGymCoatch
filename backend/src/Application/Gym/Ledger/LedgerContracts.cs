using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Ledger;

public sealed record FinancialAccountItem(
    Guid Id,
    string Name,
    FinancialAccountKind AccountKind,
    string Currency,
    bool IsActive,
    decimal Balance);

public sealed record LedgerTransactionItem(
    Guid Id,
    DateOnly EntryDate,
    string Description,
    JournalEntryType EntryType,
    string? AccountName,
    string? Category,
    decimal Amount,
    string Currency,
    JournalEntryStatus Status,
    string? RecordedBy,
    JournalSourceType SourceType,
    Guid? SourceId);

public sealed record LedgerSummary(
    decimal MoneyIn,
    decimal MoneyOut,
    decimal NetCashFlow,
    int TransactionCount,
    string Currency,
    IReadOnlyList<AccountTotal> Accounts,
    IReadOnlyList<CategoryTotal> Categories);

public sealed record AccountTotal(Guid AccountId, string AccountName, decimal MoneyIn, decimal MoneyOut, decimal Balance);

public sealed record CategoryTotal(string Category, decimal MoneyIn, decimal MoneyOut, int Count);

public sealed record LedgerTransactionSearch(
    DateOnly? From,
    DateOnly? To,
    JournalEntryType? EntryType,
    Guid? FinancialAccountId,
    string? Category,
    JournalEntryStatus? Status,
    Guid? RecordedByUserId,
    int Page,
    int PageSize);
