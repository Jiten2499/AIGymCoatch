using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class ExpensesController : ApiControllerBase
{
    public ExpensesController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("expense-categories")]
    [RequiresPermission(PermissionCodes.ExpenseRead)]
    public async Task<IActionResult> Categories(bool? activeOnly, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetExpenseCategoriesQuery(activeOnly ?? false), cancellationToken)).ToActionResult();

    [HttpPost("expense-categories")]
    [RequiresPermission(PermissionCodes.ExpenseCategoryManage)]
    public async Task<IActionResult> CreateCategory(ExpenseCategoryRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateExpenseCategoryCommand(request.Name, request.Description), cancellationToken)).ToActionResult();

    [HttpPut("expense-categories/{id:guid}")]
    [RequiresPermission(PermissionCodes.ExpenseCategoryManage)]
    public async Task<IActionResult> UpdateCategory(Guid id, ExpenseCategoryRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateExpenseCategoryCommand(id, request.Name, request.Description), cancellationToken)).ToActionResult();

    [HttpPost("expense-categories/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.ExpenseCategoryManage)]
    public async Task<IActionResult> CategoryStatus(Guid id, ExpenseCategoryStatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeExpenseCategoryStatusCommand(id, request.Active), cancellationToken)).ToActionResult();

    [HttpGet("expenses")]
    [RequiresPermission(PermissionCodes.ExpenseRead)]
    public async Task<IActionResult> Search(string? term, Guid? categoryId, ExpenseStatus? status, ExpensePaymentMethod? paymentMethod, Guid? recordedByUserId, DateOnly? from, DateOnly? to, decimal? minAmount, decimal? maxAmount, int? page, int? pageSize, string? sort, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetExpensesQuery(term, categoryId, status, paymentMethod, recordedByUserId, from, to, minAmount, maxAmount, page ?? 1, pageSize ?? 20, sort), cancellationToken)).ToActionResult();

    [HttpGet("expenses/summary")]
    [RequiresPermission(PermissionCodes.ExpenseRead)]
    public async Task<IActionResult> Summary(DateOnly? from, DateOnly? to, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetExpenseSummaryQuery(from, to), cancellationToken)).ToActionResult();

    [HttpPost("expenses")]
    [RequiresPermission(PermissionCodes.ExpenseCreate)]
    public async Task<IActionResult> Create(ExpenseRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateExpenseCommand(request.CategoryId, request.Description, request.Amount, request.ExpenseDate, request.PaymentMethod, request.Notes), cancellationToken)).ToActionResult();

    [HttpGet("expenses/{id:guid}")]
    [RequiresPermission(PermissionCodes.ExpenseRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetExpenseQuery(id), cancellationToken)).ToActionResult();

    [HttpPut("expenses/{id:guid}")]
    [RequiresPermission(PermissionCodes.ExpenseUpdate)]
    public async Task<IActionResult> Update(Guid id, ExpenseRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateExpenseCommand(id, request.CategoryId, request.Description, request.Amount, request.ExpenseDate, request.PaymentMethod, request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("expenses/{id:guid}/void")]
    [RequiresPermission(PermissionCodes.ExpenseVoid)]
    public async Task<IActionResult> Void(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new VoidExpenseCommand(id), cancellationToken)).ToActionResult();
}

public sealed record ExpenseCategoryRequest(string Name, string? Description);
public sealed record ExpenseCategoryStatusRequest(bool Active);
public sealed record ExpenseRequest(Guid CategoryId, string Description, decimal Amount, DateOnly ExpenseDate, ExpensePaymentMethod PaymentMethod, string? Notes);
