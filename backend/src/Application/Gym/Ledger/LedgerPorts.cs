using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Ledger;

public interface ILedgerRepository
{
    Task EnsureDefaultsAsync(Guid tenantId, string currency, CancellationToken cancellationToken);

    Task<FinancialAccount?> GetFinancialAccountAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<FinancialAccount?> FindActiveByKindAsync(Guid tenantId, FinancialAccountKind kind, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task AddFinancialAccountAsync(FinancialAccount account, LedgerAccount ledgerAccount, CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialAccountItem>> ListFinancialAccountsAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken);

    Task<bool> HasPostedHistoryAsync(Guid tenantId, Guid financialAccountId, CancellationToken cancellationToken);

    Task<bool> SourceExistsAsync(Guid tenantId, JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken);

    Task<JournalEntry?> GetEntryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<JournalEntry?> GetPostedBySourceAsync(Guid tenantId, JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<JournalEntryLine>> GetLinesAsync(Guid tenantId, Guid journalEntryId, CancellationToken cancellationToken);

    Task AddEntryAsync(JournalEntry entry, IReadOnlyList<JournalEntryLine> lines, CancellationToken cancellationToken);

    Task<decimal> GetAccountBalanceAsync(Guid tenantId, Guid financialAccountId, CancellationToken cancellationToken);

    Task<PageResult<LedgerTransactionItem>> SearchTransactionsAsync(Guid tenantId, LedgerTransactionSearch search, CancellationToken cancellationToken);

    Task<LedgerSummary> SummarizeAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<LedgerAccount?> GetLedgerAccountByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken);
}

public interface ILedgerPostingService
{
    Task<Result> TryPostMembershipPaymentAsync(Guid tenantId, MembershipPayment payment, Guid actorId, CancellationToken cancellationToken);

    Task<Result> TryReverseMembershipPaymentAsync(Guid tenantId, MembershipPayment payment, Guid actorId, CancellationToken cancellationToken);

    Task<Result> TryPostExpenseAsync(Guid tenantId, Expense expense, string categoryName, Guid actorId, CancellationToken cancellationToken);

    Task<Result> TryReverseExpenseAsync(Guid tenantId, Expense expense, Guid actorId, CancellationToken cancellationToken);
}
