using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class LedgerController : ApiControllerBase
{
    public LedgerController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("ledger/summary")]
    [RequiresPermission(PermissionCodes.LedgerRead)]
    public async Task<IActionResult> Summary(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetLedgerSummaryQuery(from, to), cancellationToken)).ToActionResult();

    [HttpGet("ledger/transactions")]
    [RequiresPermission(PermissionCodes.LedgerRead)]
    public async Task<IActionResult> Transactions(
        DateOnly? from,
        DateOnly? to,
        JournalEntryType? entryType,
        Guid? financialAccountId,
        string? category,
        JournalEntryStatus? status,
        Guid? recordedByUserId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
        (await Sender.Send(new ListLedgerTransactionsQuery(from, to, entryType, financialAccountId, category, status, recordedByUserId, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("ledger/transactions/{id:guid}")]
    [RequiresPermission(PermissionCodes.LedgerRead)]
    public async Task<IActionResult> GetTransaction(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetLedgerTransactionQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("ledger/transfers")]
    [RequiresPermission(PermissionCodes.LedgerCreate)]
    public async Task<IActionResult> Transfer(TransferRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateTransferCommand(request.FromAccountId, request.ToAccountId, request.Amount, request.TransferDate, request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("ledger/transactions/{id:guid}/void")]
    [RequiresPermission(PermissionCodes.LedgerVoid)]
    public async Task<IActionResult> Void(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new VoidLedgerEntryCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("financial-accounts")]
    [RequiresPermission(PermissionCodes.LedgerRead)]
    public async Task<IActionResult> Accounts(bool? activeOnly, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListFinancialAccountsQuery(activeOnly ?? false), cancellationToken)).ToActionResult();

    [HttpGet("financial-accounts/{id:guid}")]
    [RequiresPermission(PermissionCodes.LedgerRead)]
    public async Task<IActionResult> Account(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetFinancialAccountBalanceQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("financial-accounts")]
    [RequiresPermission(PermissionCodes.LedgerAccountManage)]
    public async Task<IActionResult> CreateAccount(FinancialAccountRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateFinancialAccountCommand(request.Name, request.AccountKind), cancellationToken)).ToActionResult();

    [HttpPut("financial-accounts/{id:guid}")]
    [RequiresPermission(PermissionCodes.LedgerAccountManage)]
    public async Task<IActionResult> UpdateAccount(Guid id, FinancialAccountRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateFinancialAccountCommand(id, request.Name, request.AccountKind), cancellationToken)).ToActionResult();

    [HttpPost("financial-accounts/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.LedgerAccountManage)]
    public async Task<IActionResult> AccountStatus(Guid id, AccountStatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SetFinancialAccountActiveCommand(id, request.Active), cancellationToken)).ToActionResult();
}

public sealed record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, DateOnly TransferDate, string? Notes);

public sealed record FinancialAccountRequest(string Name, FinancialAccountKind AccountKind);

public sealed record AccountStatusRequest(bool Active);
