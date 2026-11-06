using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Ledger;

public sealed class LedgerPostingService : ILedgerPostingService
{
    private readonly IFeatureFlagService _features;
    private readonly ILedgerRepository _ledger;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IDateTimeProvider _clock;

    public LedgerPostingService(
        IFeatureFlagService features,
        ILedgerRepository ledger,
        IBusinessProfileRepository profiles,
        IDateTimeProvider clock)
    {
        _features = features;
        _ledger = ledger;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result> TryPostMembershipPaymentAsync(Guid tenantId, MembershipPayment payment, Guid actorId, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        if (await _ledger.SourceExistsAsync(tenantId, JournalSourceType.MembershipPayment, payment.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        await EnsureAsync(tenantId, payment.Currency, cancellationToken).ConfigureAwait(false);
        var moneyAccount = await ResolveAccountAsync(tenantId, LedgerMethodMap.FromMembershipMethod(payment.PaymentMethod), cancellationToken).ConfigureAwait(false);
        var revenue = await _ledger.GetLedgerAccountByCodeAsync(tenantId, LedgerAccountCodes.MembershipRevenue, cancellationToken).ConfigureAwait(false);
        if (moneyAccount is null || revenue is null)
        {
            return Result.Failure(GymErrors.InvalidState);
        }

        var created = JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            payment.PaymentDate,
            $"Membership payment {payment.ReferenceNumber ?? payment.Id.ToString("N")[..8]}",
            JournalEntryType.MoneyIn,
            JournalSourceType.MembershipPayment,
            payment.Id,
            "Membership",
            moneyAccount.Id,
            null,
            payment.Amount,
            payment.Currency,
            actorId,
            _clock.UtcNow,
            null,
            [
                (moneyAccount.LedgerAccountId, payment.Amount, 0m),
                (revenue.Id, 0m, payment.Amount),
            ]);
        await _ledger.AddEntryAsync(created.Entry, created.Lines, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public Task<Result> TryReverseMembershipPaymentAsync(Guid tenantId, MembershipPayment payment, Guid actorId, CancellationToken cancellationToken) =>
        ReverseSourceAsync(tenantId, JournalSourceType.MembershipPayment, payment.Id, actorId, cancellationToken);

    public async Task<Result> TryPostExpenseAsync(Guid tenantId, Expense expense, string categoryName, Guid actorId, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        if (await _ledger.SourceExistsAsync(tenantId, JournalSourceType.Expense, expense.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        await EnsureAsync(tenantId, expense.Currency, cancellationToken).ConfigureAwait(false);
        var moneyAccount = await ResolveAccountAsync(tenantId, LedgerMethodMap.FromExpenseMethod(expense.PaymentMethod), cancellationToken).ConfigureAwait(false);
        var expenseAccount = await _ledger.GetLedgerAccountByCodeAsync(tenantId, LedgerAccountCodes.OperatingExpense, cancellationToken).ConfigureAwait(false);
        if (moneyAccount is null || expenseAccount is null)
        {
            return Result.Failure(GymErrors.InvalidState);
        }

        var created = JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            expense.ExpenseDate,
            expense.Description,
            JournalEntryType.MoneyOut,
            JournalSourceType.Expense,
            expense.Id,
            categoryName,
            moneyAccount.Id,
            null,
            expense.Amount,
            expense.Currency,
            actorId,
            _clock.UtcNow,
            null,
            [
                (expenseAccount.Id, expense.Amount, 0m),
                (moneyAccount.LedgerAccountId, 0m, expense.Amount),
            ]);
        await _ledger.AddEntryAsync(created.Entry, created.Lines, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public Task<Result> TryReverseExpenseAsync(Guid tenantId, Expense expense, Guid actorId, CancellationToken cancellationToken) =>
        ReverseSourceAsync(tenantId, JournalSourceType.Expense, expense.Id, actorId, cancellationToken);

    private async Task<Result> ReverseSourceAsync(Guid tenantId, JournalSourceType sourceType, Guid sourceId, Guid actorId, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        var original = await _ledger.GetPostedBySourceAsync(tenantId, sourceType, sourceId, cancellationToken).ConfigureAwait(false);
        if (original is null)
        {
            return Result.Success();
        }

        var lines = await _ledger.GetLinesAsync(tenantId, original.Id, cancellationToken).ConfigureAwait(false);
        if (!original.Void(actorId, _clock.UtcNow))
        {
            return Result.Failure(GymErrors.InvalidState);
        }

        var reversalLines = lines
            .Select(line => (line.LedgerAccountId, Debit: line.CreditAmount, Credit: line.DebitAmount))
            .ToList();
        var created = JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            original.EntryDate,
            $"Reversal: {original.Description}",
            original.EntryType,
            JournalSourceType.Reversal,
            original.Id,
            original.Category,
            original.FinancialAccountId,
            original.CounterFinancialAccountId,
            original.Amount,
            original.Currency,
            actorId,
            _clock.UtcNow,
            original.Id,
            reversalLines);
        await _ledger.AddEntryAsync(created.Entry, created.Lines, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task EnsureAsync(Guid tenantId, string currency, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        await _ledger.EnsureDefaultsAsync(tenantId, string.IsNullOrWhiteSpace(currency) ? profile?.Currency ?? "USD" : currency, cancellationToken).ConfigureAwait(false);
    }

    private async Task<FinancialAccount?> ResolveAccountAsync(Guid tenantId, FinancialAccountKind kind, CancellationToken cancellationToken)
    {
        var match = await _ledger.FindActiveByKindAsync(tenantId, kind, cancellationToken).ConfigureAwait(false);
        if (match is not null)
        {
            return match;
        }

        return await _ledger.FindActiveByKindAsync(tenantId, FinancialAccountKind.Cash, cancellationToken).ConfigureAwait(false)
            ?? await _ledger.FindActiveByKindAsync(tenantId, FinancialAccountKind.Other, cancellationToken).ConfigureAwait(false);
    }
}
