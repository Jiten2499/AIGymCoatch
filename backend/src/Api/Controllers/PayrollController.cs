using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Payroll;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class PayrollController : ApiControllerBase
{
    public PayrollController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("payroll/runs")]
    [RequiresPermission(PermissionCodes.PayrollRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListPayrollRunsQuery(), cancellationToken)).ToActionResult();

    [HttpGet("payroll/runs/{id:guid}")]
    [RequiresPermission(PermissionCodes.PayrollRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetPayrollRunQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("payroll/runs/prepare")]
    [RequiresPermission(PermissionCodes.PayrollPrepare)]
    public async Task<IActionResult> Prepare(PreparePayrollRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new PreparePayrollCommand(request.Year, request.Month), cancellationToken)).ToActionResult();

    [HttpPost("payroll/runs/{id:guid}/submit")]
    [RequiresPermission(PermissionCodes.PayrollPrepare)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new SubmitPayrollReviewCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("payroll/runs/{id:guid}/draft")]
    [RequiresPermission(PermissionCodes.PayrollPrepare)]
    public async Task<IActionResult> Draft(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ReturnPayrollToDraftCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("payroll/runs/{id:guid}/approve")]
    [RequiresPermission(PermissionCodes.PayrollApprove)]
    public async Task<IActionResult> Approve(Guid id, ApprovePayrollRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ApprovePayrollCommand(id, request.Confirm), cancellationToken)).ToActionResult();

    [HttpPost("payroll/lines/{lineId:guid}/adjustments")]
    [RequiresPermission(PermissionCodes.PayrollPrepare)]
    public async Task<IActionResult> AddAdjustment(Guid lineId, AddPayrollAdjustmentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddPayrollAdjustmentCommand(lineId, request.Kind, request.Label, request.Amount, request.Reason), cancellationToken)).ToActionResult();

    [HttpDelete("payroll/adjustments/{id:guid}")]
    [RequiresPermission(PermissionCodes.PayrollPrepare)]
    public async Task<IActionResult> RemoveAdjustment(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RemovePayrollAdjustmentCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("payroll/lines/{lineId:guid}/payments")]
    [RequiresPermission(PermissionCodes.PayrollPay)]
    public async Task<IActionResult> Pay(Guid lineId, RecordPayrollPaymentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RecordPayrollPaymentCommand(lineId, request.Amount, request.PaymentDate, request.Method, request.Reference), cancellationToken)).ToActionResult();

    [HttpPost("payroll/runs/{id:guid}/payments/bulk")]
    [RequiresPermission(PermissionCodes.PayrollPay)]
    public async Task<IActionResult> BulkPay(Guid id, BulkPayrollPaymentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RecordBulkPayrollPaymentsCommand(id, request.PaymentDate, request.Method, request.ReferencePrefix), cancellationToken)).ToActionResult();

    [HttpGet("payroll/lines/{lineId:guid}/payslip")]
    [RequiresPermission(PermissionCodes.PayrollRead)]
    public async Task<IActionResult> Payslip(Guid lineId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetPayslipQuery(lineId), cancellationToken)).ToActionResult();

    [HttpGet("payroll/compensation")]
    [RequiresPermission(PermissionCodes.PayrollRead)]
    public async Task<IActionResult> Compensation(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListStaffCompensationQuery(), cancellationToken)).ToActionResult();

    [HttpPut("payroll/compensation/{staffProfileId:guid}")]
    [RequiresPermission(PermissionCodes.PayrollPrepare)]
    public async Task<IActionResult> SaveCompensation(Guid staffProfileId, SaveStaffCompensationRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SaveStaffCompensationCommand(staffProfileId, request.BaseSalary, request.Allowances, request.EffectiveFrom, request.Notes), cancellationToken)).ToActionResult();
}

public sealed record PreparePayrollRequest(int Year, int Month);
public sealed record ApprovePayrollRequest(bool Confirm);
public sealed record AddPayrollAdjustmentRequest(PayrollAdjustmentKind Kind, string Label, decimal Amount, string Reason);
public sealed record RecordPayrollPaymentRequest(decimal Amount, DateOnly PaymentDate, PayrollPaymentMethod Method, string? Reference);
public sealed record BulkPayrollPaymentRequest(DateOnly PaymentDate, PayrollPaymentMethod Method, string? ReferencePrefix);
public sealed record SaveStaffCompensationRequest(decimal BaseSalary, decimal Allowances, DateOnly EffectiveFrom, string? Notes);
