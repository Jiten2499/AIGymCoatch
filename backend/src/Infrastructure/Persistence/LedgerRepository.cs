using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class LedgerRepository : ILedgerRepository
{
    private readonly ApplicationDbContext _db;

    public LedgerRepository(ApplicationDbContext db) => _db = db;

    public async Task EnsureDefaultsAsync(Guid tenantId, string currency, CancellationToken cancellationToken)
    {
        currency = currency.ToUpperInvariant();
        await EnsureSystemLedgerAsync(tenantId, LedgerAccountCodes.MembershipRevenue, "Membership Revenue", LedgerAccountClass.Revenue, cancellationToken).ConfigureAwait(false);
        await EnsureSystemLedgerAsync(tenantId, LedgerAccountCodes.PtRevenue, "Personal Training Revenue", LedgerAccountClass.Revenue, cancellationToken).ConfigureAwait(false);
        await EnsureSystemLedgerAsync(tenantId, LedgerAccountCodes.OperatingExpense, "Operating Expense", LedgerAccountClass.Expense, cancellationToken).ConfigureAwait(false);

        foreach (var (kind, name) in new (FinancialAccountKind, string)[]
                 {
                     (FinancialAccountKind.Cash, "Cash"),
                     (FinancialAccountKind.Upi, "UPI"),
                     (FinancialAccountKind.Card, "Card/POS"),
                     (FinancialAccountKind.Bank, "Bank"),
                 })
        {
            if (UnsavedFinancialAccount(tenantId, kind) is not null
                || await _db.FinancialAccounts.AnyAsync(item => item.TenantId == tenantId && item.AccountKind == kind, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var id = Guid.NewGuid();
            var ledger = LedgerAccount.Create(Guid.NewGuid(), tenantId, LedgerAccountCodes.AssetFor(id), name, LedgerAccountClass.Asset, true);
            var account = FinancialAccount.Create(id, tenantId, name, kind, currency, ledger.Id);
            await _db.LedgerAccounts.AddAsync(ledger, cancellationToken).ConfigureAwait(false);
            await _db.FinancialAccounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<FinancialAccount?> GetFinancialAccountAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.FinancialAccounts.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public async Task<FinancialAccount?> FindActiveByKindAsync(Guid tenantId, FinancialAccountKind kind, CancellationToken cancellationToken)
    {
        var pending = UnsavedFinancialAccount(tenantId, kind);
        if (pending is { IsActive: true })
        {
            return pending;
        }

        return await _db.FinancialAccounts
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.AccountKind == kind && item.IsActive, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> NameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken) =>
        _db.FinancialAccounts.AnyAsync(
            item => item.TenantId == tenantId && item.Name == name && (excludeId == null || item.Id != excludeId),
            cancellationToken);

    public async Task AddFinancialAccountAsync(FinancialAccount account, LedgerAccount ledgerAccount, CancellationToken cancellationToken)
    {
        await _db.LedgerAccounts.AddAsync(ledgerAccount, cancellationToken).ConfigureAwait(false);
        await _db.FinancialAccounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FinancialAccountItem>> ListFinancialAccountsAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _db.FinancialAccounts.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (activeOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        var accounts = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        var balances = new List<FinancialAccountItem>(accounts.Count);
        foreach (var account in accounts)
        {
            balances.Add(new FinancialAccountItem(
                account.Id,
                account.Name,
                account.AccountKind,
                account.Currency,
                account.IsActive,
                await GetAccountBalanceAsync(tenantId, account.Id, cancellationToken).ConfigureAwait(false)));
        }

        return balances;
    }

    public Task<bool> HasPostedHistoryAsync(Guid tenantId, Guid financialAccountId, CancellationToken cancellationToken) =>
        _db.JournalEntries.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId
                && item.Status == JournalEntryStatus.Posted
                && (item.FinancialAccountId == financialAccountId || item.CounterFinancialAccountId == financialAccountId),
            cancellationToken);

    public Task<bool> SourceExistsAsync(Guid tenantId, JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken) =>
        _db.JournalEntries.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId
                && item.SourceType == sourceType
                && item.SourceId == sourceId
                && item.Status == JournalEntryStatus.Posted,
            cancellationToken);

    public Task<JournalEntry?> GetEntryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.JournalEntries.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public Task<JournalEntry?> GetPostedBySourceAsync(Guid tenantId, JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken) =>
        _db.JournalEntries.FirstOrDefaultAsync(
            item => item.TenantId == tenantId
                && item.SourceType == sourceType
                && item.SourceId == sourceId
                && item.Status == JournalEntryStatus.Posted,
            cancellationToken);

    public async Task<IReadOnlyList<JournalEntryLine>> GetLinesAsync(Guid tenantId, Guid journalEntryId, CancellationToken cancellationToken) =>
        await _db.JournalEntryLines.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.JournalEntryId == journalEntryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddEntryAsync(JournalEntry entry, IReadOnlyList<JournalEntryLine> lines, CancellationToken cancellationToken)
    {
        await _db.JournalEntries.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        foreach (var line in lines)
        {
            await _db.JournalEntryLines.AddAsync(line, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<decimal> GetAccountBalanceAsync(Guid tenantId, Guid financialAccountId, CancellationToken cancellationToken)
    {
        var account = await _db.FinancialAccounts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == financialAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return 0m;
        }

        var ledgerId = account.LedgerAccountId;
        var debits = await _db.JournalEntryLines.AsNoTracking()
            .Where(line => line.TenantId == tenantId && line.LedgerAccountId == ledgerId)
            .Join(_db.JournalEntries.AsNoTracking().Where(entry => entry.TenantId == tenantId && entry.Status == JournalEntryStatus.Posted),
                line => line.JournalEntryId,
                entry => entry.Id,
                (line, _) => line.DebitAmount)
            .SumAsync(cancellationToken)
            .ConfigureAwait(false);
        var credits = await _db.JournalEntryLines.AsNoTracking()
            .Where(line => line.TenantId == tenantId && line.LedgerAccountId == ledgerId)
            .Join(_db.JournalEntries.AsNoTracking().Where(entry => entry.TenantId == tenantId && entry.Status == JournalEntryStatus.Posted),
                line => line.JournalEntryId,
                entry => entry.Id,
                (line, _) => line.CreditAmount)
            .SumAsync(cancellationToken)
            .ConfigureAwait(false);
        return debits - credits;
    }

    public async Task<PageResult<LedgerTransactionItem>> SearchTransactionsAsync(Guid tenantId, LedgerTransactionSearch search, CancellationToken cancellationToken)
    {
        var query =
            from entry in _db.JournalEntries.AsNoTracking()
            join user in _db.Users.AsNoTracking() on entry.RecordedByUserId equals user.Id
            join account in _db.FinancialAccounts.AsNoTracking() on entry.FinancialAccountId equals account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            where entry.TenantId == tenantId
            select new { entry, Receiver = user.FirstName + " " + user.LastName, AccountName = account != null ? account.Name : null };

        if (search.From is DateOnly from)
        {
            query = query.Where(row => row.entry.EntryDate >= from);
        }

        if (search.To is DateOnly to)
        {
            query = query.Where(row => row.entry.EntryDate <= to);
        }

        if (search.EntryType is JournalEntryType type)
        {
            query = query.Where(row => row.entry.EntryType == type);
        }

        if (search.FinancialAccountId is Guid accountId)
        {
            query = query.Where(row => row.entry.FinancialAccountId == accountId || row.entry.CounterFinancialAccountId == accountId);
        }

        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            var category = search.Category.Trim();
            query = query.Where(row => row.entry.Category == category);
        }

        if (search.Status is JournalEntryStatus status)
        {
            query = query.Where(row => row.entry.Status == status);
        }

        if (search.RecordedByUserId is Guid recordedBy)
        {
            query = query.Where(row => row.entry.RecordedByUserId == recordedBy);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = new PageQuery(search.Page, search.PageSize);
        var items = await query
            .OrderByDescending(row => row.entry.EntryDate)
            .ThenByDescending(row => row.entry.CreatedAt)
            .Skip(page.Skip)
            .Take(page.NormalizedSize)
            .Select(row => new LedgerTransactionItem(
                row.entry.Id,
                row.entry.EntryDate,
                row.entry.Description,
                row.entry.EntryType,
                row.AccountName,
                row.entry.Category,
                row.entry.Amount,
                row.entry.Currency,
                row.entry.Status,
                row.Receiver,
                row.entry.SourceType,
                row.entry.SourceId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PageResult<LedgerTransactionItem>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<LedgerSummary> SummarizeAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var posted = _db.JournalEntries.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.Status == JournalEntryStatus.Posted
                && item.SourceType != JournalSourceType.Reversal
                && item.EntryDate >= from
                && item.EntryDate <= to);
        var moneyIn = await posted.Where(item => item.EntryType == JournalEntryType.MoneyIn).SumAsync(item => (decimal?)item.Amount, cancellationToken).ConfigureAwait(false) ?? 0m;
        var moneyOut = await posted.Where(item => item.EntryType == JournalEntryType.MoneyOut).SumAsync(item => (decimal?)item.Amount, cancellationToken).ConfigureAwait(false) ?? 0m;
        var count = await posted.CountAsync(cancellationToken).ConfigureAwait(false);
        var currency = await _db.FinancialAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.Currency)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? "USD";

        var accountRows = await (
            from entry in posted
            where entry.FinancialAccountId != null
            join account in _db.FinancialAccounts.AsNoTracking() on entry.FinancialAccountId equals account.Id
            group entry by new { account.Id, account.Name } into g
            select new
            {
                g.Key.Id,
                g.Key.Name,
                MoneyIn = g.Sum(item => item.EntryType == JournalEntryType.MoneyIn ? item.Amount : 0m),
                MoneyOut = g.Sum(item => item.EntryType == JournalEntryType.MoneyOut ? item.Amount : 0m),
            }).ToListAsync(cancellationToken).ConfigureAwait(false);

        var accounts = new List<AccountTotal>();
        foreach (var row in accountRows.OrderBy(item => item.Name))
        {
            accounts.Add(new AccountTotal(row.Id, row.Name, row.MoneyIn, row.MoneyOut, await GetAccountBalanceAsync(tenantId, row.Id, cancellationToken).ConfigureAwait(false)));
        }

        var categoryRows = await posted
            .Where(item => item.Category != null)
            .GroupBy(item => item.Category!)
            .Select(group => new
            {
                Category = group.Key,
                MoneyIn = group.Sum(item => item.EntryType == JournalEntryType.MoneyIn ? item.Amount : 0m),
                MoneyOut = group.Sum(item => item.EntryType == JournalEntryType.MoneyOut ? item.Amount : 0m),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var categories = categoryRows
            .OrderBy(item => item.Category)
            .Select(item => new CategoryTotal(item.Category, item.MoneyIn, item.MoneyOut, item.Count))
            .ToList();

        return new LedgerSummary(moneyIn, moneyOut, moneyIn - moneyOut, count, currency, accounts, categories);
    }

    public async Task<LedgerAccount?> GetLedgerAccountByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var pending = _db.LedgerAccounts.Local.FirstOrDefault(item =>
            item.TenantId == tenantId
            && item.Code == normalized
            && _db.Entry(item).State != EntityState.Deleted);
        if (pending is not null)
        {
            return pending;
        }

        return await _db.LedgerAccounts
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Code == normalized, cancellationToken)
            .ConfigureAwait(false);
    }

    private FinancialAccount? UnsavedFinancialAccount(Guid tenantId, FinancialAccountKind kind) =>
        _db.FinancialAccounts.Local.FirstOrDefault(item =>
            item.TenantId == tenantId
            && item.AccountKind == kind
            && _db.Entry(item).State != EntityState.Deleted);

    private async Task EnsureSystemLedgerAsync(Guid tenantId, string code, string name, LedgerAccountClass accountClass, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (_db.LedgerAccounts.Local.Any(item => item.TenantId == tenantId && item.Code == normalized && _db.Entry(item).State != EntityState.Deleted)
            || await _db.LedgerAccounts.AnyAsync(item => item.TenantId == tenantId && item.Code == normalized, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await _db.LedgerAccounts.AddAsync(LedgerAccount.Create(Guid.NewGuid(), tenantId, code, name, accountClass, true), cancellationToken).ConfigureAwait(false);
    }
}
