using System.Collections.Concurrent;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Ledger;

public sealed class LedgerPostingService : ILedgerPostingService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> PostingGates = new();

    private readonly IFeatureFlagService _features;
    private readonly ILedgerRepository _ledger;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public LedgerPostingService(
        IFeatureFlagService features,
        ILedgerRepository ledger,
        IBusinessProfileRepository profiles,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _features = features;
        _ledger = ledger;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
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

    public async Task<Result<bool>> TryPostPtPackagePaymentAsync(Guid tenantId, MemberPtAssignment assignment, Guid actorId, CancellationToken cancellationToken)
    {
        if (!Payable(assignment) || !await LedgerEnabledAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(false);
        }

        return await PostPtPackageAsync(tenantId, assignment, assignment.Id, assignment.AmountPaid, assignment.PaymentMethod!.Value, assignment.PaymentDate!.Value, assignment.PaymentReferenceNumber, actorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<bool>> TryPostPtCollectionAsync(Guid tenantId, MemberPtAssignment assignment, MemberPtPayment payment, Guid actorId, CancellationToken cancellationToken)
    {
        if (payment.Amount <= 0 || !await LedgerEnabledAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(false);
        }

        return await PostPtPackageAsync(tenantId, assignment, payment.Id, payment.Amount, payment.PaymentMethod, payment.PaymentDate, payment.ReferenceNumber, actorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<bool>> PostOutstandingPtPaymentsAsync(Guid tenantId, Guid actorId, IReadOnlyList<MemberPtAssignment> assignments, CancellationToken cancellationToken)
    {
        var gate = PostingGates.GetOrAdd(tenantId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await LedgerEnabledAsync(tenantId, cancellationToken).ConfigureAwait(false))
            {
                return Result.Success(false);
            }

            var posted = false;
            foreach (var assignment in assignments)
            {
                if (!Payable(assignment))
                {
                    continue;
                }

                var result = await PostPtPackageAsync(tenantId, assignment, assignment.Id, assignment.AmountPaid, assignment.PaymentMethod!.Value, assignment.PaymentDate!.Value, assignment.PaymentReferenceNumber, assignment.CreatedBy ?? actorId, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    return result;
                }

                posted |= result.Value;
            }

            if (posted)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result.Success(posted);
        }
        finally
        {
            gate.Release();
        }
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

    private async Task<Result<bool>> PostPtPackageAsync(Guid tenantId, MemberPtAssignment assignment, Guid sourceId, decimal amount, MembershipPaymentMethod method, DateOnly paymentDate, string? referenceNumber, Guid actorId, CancellationToken cancellationToken)
    {
        if (await _ledger.SourceExistsAsync(tenantId, JournalSourceType.PtPackagePayment, sourceId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(false);
        }

        await EnsureAsync(tenantId, assignment.Currency, cancellationToken).ConfigureAwait(false);
        var moneyAccount = await ResolveAccountAsync(tenantId, LedgerMethodMap.FromMembershipMethod(method), cancellationToken).ConfigureAwait(false);
        var revenue = await _ledger.GetLedgerAccountByCodeAsync(tenantId, LedgerAccountCodes.PtRevenue, cancellationToken).ConfigureAwait(false);
        if (moneyAccount is null || revenue is null)
        {
            return Result.Failure<bool>(GymErrors.InvalidState);
        }

        var reference = string.IsNullOrWhiteSpace(referenceNumber) ? sourceId.ToString("N")[..8] : referenceNumber.Trim();
        var created = JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            paymentDate,
            $"PT package {assignment.PackageName} {reference}",
            JournalEntryType.MoneyIn,
            JournalSourceType.PtPackagePayment,
            sourceId,
            "Personal training",
            moneyAccount.Id,
            null,
            amount,
            assignment.Currency,
            actorId,
            _clock.UtcNow,
            null,
            [
                (moneyAccount.LedgerAccountId, amount, 0m),
                (revenue.Id, 0m, amount),
            ]);
        await _ledger.AddEntryAsync(created.Entry, created.Lines, cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    private Task<bool> LedgerEnabledAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _features.IsEnabledAsync(tenantId, FeatureCodes.GymFinancialLedger, cancellationToken);

    private static bool Payable(MemberPtAssignment assignment) =>
        assignment.AmountPaid > 0 && assignment.PaymentMethod is not null && assignment.PaymentDate is not null;

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
