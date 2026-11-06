using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Ledger;

public sealed record ListFinancialAccountsQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<FinancialAccountItem>>>;

public sealed record GetFinancialAccountBalanceQuery(Guid Id) : IRequest<Result<FinancialAccountItem>>;

public sealed record ListLedgerTransactionsQuery(
    DateOnly? From,
    DateOnly? To,
    JournalEntryType? EntryType,
    Guid? FinancialAccountId,
    string? Category,
    JournalEntryStatus? Status,
    Guid? RecordedByUserId,
    int Page,
    int PageSize) : IRequest<Result<PageResult<LedgerTransactionItem>>>;

public sealed record GetLedgerSummaryQuery(DateOnly From, DateOnly To) : IRequest<Result<LedgerSummary>>;

public sealed record GetLedgerTransactionQuery(Guid Id) : IRequest<Result<LedgerTransactionItem>>;

public sealed class LedgerQueryHandler :
    IRequestHandler<ListFinancialAccountsQuery, Result<IReadOnlyList<FinancialAccountItem>>>,
    IRequestHandler<GetFinancialAccountBalanceQuery, Result<FinancialAccountItem>>,
    IRequestHandler<ListLedgerTransactionsQuery, Result<PageResult<LedgerTransactionItem>>>,
    IRequestHandler<GetLedgerSummaryQuery, Result<LedgerSummary>>,
    IRequestHandler<GetLedgerTransactionQuery, Result<LedgerTransactionItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILedgerRepository _ledger;
    private readonly IBusinessProfileRepository _profiles;

    public LedgerQueryHandler(IOperationGuard guard, ICurrentActor actor, ILedgerRepository ledger, IBusinessProfileRepository profiles)
    {
        _guard = guard;
        _actor = actor;
        _ledger = ledger;
        _profiles = profiles;
    }

    public async Task<Result<IReadOnlyList<FinancialAccountItem>>> Handle(ListFinancialAccountsQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<IReadOnlyList<FinancialAccountItem>>(opened.Error);
        }

        await EnsureAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(await _ledger.ListFinancialAccountsAsync(opened.Value, request.ActiveOnly, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<FinancialAccountItem>> Handle(GetFinancialAccountBalanceQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<FinancialAccountItem>(opened.Error);
        }

        var account = await _ledger.GetFinancialAccountAsync(opened.Value, request.Id, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Result.Failure<FinancialAccountItem>(GymErrors.NotFound);
        }

        var balance = await _ledger.GetAccountBalanceAsync(opened.Value, account.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new FinancialAccountItem(account.Id, account.Name, account.AccountKind, account.Currency, account.IsActive, balance));
    }

    public async Task<Result<PageResult<LedgerTransactionItem>>> Handle(ListLedgerTransactionsQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PageResult<LedgerTransactionItem>>(opened.Error);
        }

        await EnsureAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        var page = await _ledger.SearchTransactionsAsync(
            opened.Value,
            new LedgerTransactionSearch(request.From, request.To, request.EntryType, request.FinancialAccountId, request.Category, request.Status, request.RecordedByUserId, request.Page, request.PageSize),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }

    public async Task<Result<LedgerSummary>> Handle(GetLedgerSummaryQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<LedgerSummary>(opened.Error);
        }

        if (request.To < request.From)
        {
            return Result.Failure<LedgerSummary>(GymErrors.InvalidState);
        }

        await EnsureAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(await _ledger.SummarizeAsync(opened.Value, request.From, request.To, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<LedgerTransactionItem>> Handle(GetLedgerTransactionQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<LedgerTransactionItem>(opened.Error);
        }

        var entry = await _ledger.GetEntryAsync(opened.Value, request.Id, cancellationToken).ConfigureAwait(false);
        return entry is null
            ? Result.Failure<LedgerTransactionItem>(GymErrors.NotFound)
            : Result.Success(new LedgerTransactionItem(entry.Id, entry.EntryDate, entry.Description, entry.EntryType, null, entry.Category, entry.Amount, entry.Currency, entry.Status, null, entry.SourceType, entry.SourceId));
    }

    private async Task<Result<Guid>> OpenAsync(CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.LedgerRead, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(gate.Error);
        }

        return Result.Success(tenantId);
    }

    private async Task EnsureAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        await _ledger.EnsureDefaultsAsync(tenantId, profile?.Currency ?? "USD", cancellationToken).ConfigureAwait(false);
    }
}
