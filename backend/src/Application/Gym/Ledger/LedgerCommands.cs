using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Ledger;

public sealed record CreateFinancialAccountCommand(string Name, FinancialAccountKind AccountKind) : IRequest<Result<FinancialAccountItem>>;

public sealed record UpdateFinancialAccountCommand(Guid Id, string Name, FinancialAccountKind AccountKind) : IRequest<Result<FinancialAccountItem>>;

public sealed record SetFinancialAccountActiveCommand(Guid Id, bool Active) : IRequest<Result<FinancialAccountItem>>;

public sealed record CreateTransferCommand(Guid FromAccountId, Guid ToAccountId, decimal Amount, DateOnly TransferDate, string? Notes) : IRequest<Result<LedgerTransactionItem>>;

public sealed record VoidLedgerEntryCommand(Guid Id) : IRequest<Result<LedgerTransactionItem>>;

public sealed class CreateFinancialAccountCommandValidator : AbstractValidator<CreateFinancialAccountCommand>
{
    public CreateFinancialAccountCommandValidator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
}

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferCommandValidator()
    {
        RuleFor(command => command.FromAccountId).NotEmpty();
        RuleFor(command => command.ToAccountId).NotEmpty().NotEqual(command => command.FromAccountId);
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Notes).MaximumLength(500);
    }
}

public sealed class LedgerCommandHandler :
    IRequestHandler<CreateFinancialAccountCommand, Result<FinancialAccountItem>>,
    IRequestHandler<UpdateFinancialAccountCommand, Result<FinancialAccountItem>>,
    IRequestHandler<SetFinancialAccountActiveCommand, Result<FinancialAccountItem>>,
    IRequestHandler<CreateTransferCommand, Result<LedgerTransactionItem>>,
    IRequestHandler<VoidLedgerEntryCommand, Result<LedgerTransactionItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILedgerRepository _ledger;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public LedgerCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ILedgerRepository ledger,
        IBusinessProfileRepository profiles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _ledger = ledger;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<FinancialAccountItem>> Handle(CreateFinancialAccountCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.LedgerAccountManage, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<FinancialAccountItem>(gate.Error);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var currency = profile?.Currency ?? "USD";
        await _ledger.EnsureDefaultsAsync(tenantId, currency, cancellationToken).ConfigureAwait(false);
        if (await _ledger.NameExistsAsync(tenantId, request.Name.Trim(), null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<FinancialAccountItem>(GymErrors.Conflict);
        }

        var id = Guid.NewGuid();
        var ledgerAccount = LedgerAccount.Create(Guid.NewGuid(), tenantId, LedgerAccountCodes.AssetFor(id), request.Name.Trim(), LedgerAccountClass.Asset, false);
        var account = FinancialAccount.Create(id, tenantId, request.Name, request.AccountKind, currency, ledgerAccount.Id);
        await _ledger.AddFinancialAccountAsync(account, ledgerAccount, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "FinancialAccountCreated", "FinancialAccount", account.Id, null, account.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new FinancialAccountItem(account.Id, account.Name, account.AccountKind, account.Currency, account.IsActive, 0m));
    }

    public async Task<Result<FinancialAccountItem>> Handle(UpdateFinancialAccountCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.LedgerAccountManage, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<FinancialAccountItem>(gate.Error);
        }

        var account = await _ledger.GetFinancialAccountAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Result.Failure<FinancialAccountItem>(GymErrors.NotFound);
        }

        if (await _ledger.NameExistsAsync(tenantId, request.Name.Trim(), account.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<FinancialAccountItem>(GymErrors.Conflict);
        }

        account.Update(request.Name, request.AccountKind);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "FinancialAccountUpdated", "FinancialAccount", account.Id, null, account.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var balance = await _ledger.GetAccountBalanceAsync(tenantId, account.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new FinancialAccountItem(account.Id, account.Name, account.AccountKind, account.Currency, account.IsActive, balance));
    }

    public async Task<Result<FinancialAccountItem>> Handle(SetFinancialAccountActiveCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.LedgerAccountManage, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<FinancialAccountItem>(gate.Error);
        }

        var account = await _ledger.GetFinancialAccountAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Result.Failure<FinancialAccountItem>(GymErrors.NotFound);
        }

        if (!request.Active && await _ledger.HasPostedHistoryAsync(tenantId, account.Id, cancellationToken).ConfigureAwait(false))
        {
            // Deactivate allowed; hard delete is not. Spec: do not allow deletion with history — deactivate is fine.
        }

        account.SetActive(request.Active);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "FinancialAccountStatusChanged", "FinancialAccount", account.Id, null, request.Active.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var balance = await _ledger.GetAccountBalanceAsync(tenantId, account.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new FinancialAccountItem(account.Id, account.Name, account.AccountKind, account.Currency, account.IsActive, balance));
    }

    public async Task<Result<LedgerTransactionItem>> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.LedgerCreate, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LedgerTransactionItem>(gate.Error);
        }

        var from = await _ledger.GetFinancialAccountAsync(tenantId, request.FromAccountId, cancellationToken).ConfigureAwait(false);
        var to = await _ledger.GetFinancialAccountAsync(tenantId, request.ToAccountId, cancellationToken).ConfigureAwait(false);
        if (from is null || to is null || !from.IsActive || !to.IsActive)
        {
            return Result.Failure<LedgerTransactionItem>(GymErrors.NotFound);
        }

        if (!string.Equals(from.Currency, to.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<LedgerTransactionItem>(GymErrors.InvalidState);
        }

        var transferId = Guid.NewGuid();
        var created = JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            request.TransferDate,
            string.IsNullOrWhiteSpace(request.Notes) ? $"Transfer {from.Name} → {to.Name}" : request.Notes.Trim(),
            JournalEntryType.Transfer,
            JournalSourceType.Transfer,
            transferId,
            "Transfer",
            to.Id,
            from.Id,
            request.Amount,
            from.Currency,
            userId,
            _clock.UtcNow,
            null,
            [
                (to.LedgerAccountId, request.Amount, 0m),
                (from.LedgerAccountId, 0m, request.Amount),
            ]);
        await _ledger.AddEntryAsync(created.Entry, created.Lines, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LedgerTransferCreated", "JournalEntry", created.Entry.Id, from.Id.ToString(), $"{request.Amount} {from.Currency}", _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new LedgerTransactionItem(
            created.Entry.Id,
            created.Entry.EntryDate,
            created.Entry.Description,
            created.Entry.EntryType,
            to.Name,
            created.Entry.Category,
            created.Entry.Amount,
            created.Entry.Currency,
            created.Entry.Status,
            null,
            created.Entry.SourceType,
            created.Entry.SourceId));
    }

    public async Task<Result<LedgerTransactionItem>> Handle(VoidLedgerEntryCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.LedgerVoid, FeatureCodes.GymFinancialLedger, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LedgerTransactionItem>(gate.Error);
        }

        var entry = await _ledger.GetEntryAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return Result.Failure<LedgerTransactionItem>(GymErrors.NotFound);
        }

        if (entry.SourceType is JournalSourceType.MembershipPayment or JournalSourceType.Expense or JournalSourceType.PtPackagePayment)
        {
            return Result.Failure<LedgerTransactionItem>(GymErrors.InvalidState);
        }

        var lines = await _ledger.GetLinesAsync(tenantId, entry.Id, cancellationToken).ConfigureAwait(false);
        if (!entry.Void(userId, _clock.UtcNow))
        {
            return Result.Failure<LedgerTransactionItem>(GymErrors.InvalidState);
        }

        var reversalLines = lines.Select(line => (line.LedgerAccountId, Debit: line.CreditAmount, Credit: line.DebitAmount)).ToList();
        var created = JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            entry.EntryDate,
            $"Reversal: {entry.Description}",
            entry.EntryType,
            JournalSourceType.Reversal,
            entry.Id,
            entry.Category,
            entry.FinancialAccountId,
            entry.CounterFinancialAccountId,
            entry.Amount,
            entry.Currency,
            userId,
            _clock.UtcNow,
            entry.Id,
            reversalLines);
        await _ledger.AddEntryAsync(created.Entry, created.Lines, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LedgerEntryVoided", "JournalEntry", entry.Id, null, entry.Amount.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new LedgerTransactionItem(
            entry.Id,
            entry.EntryDate,
            entry.Description,
            entry.EntryType,
            null,
            entry.Category,
            entry.Amount,
            entry.Currency,
            entry.Status,
            null,
            entry.SourceType,
            entry.SourceId));
    }
}
