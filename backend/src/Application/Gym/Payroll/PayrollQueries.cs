using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Payroll;

public sealed record ListPayrollRunsQuery : IRequest<Result<IReadOnlyList<PayrollRunSummary>>>;

public sealed record GetPayrollRunQuery(Guid RunId) : IRequest<Result<PayrollRunDetails>>;

public sealed record GetPayslipQuery(Guid LineId) : IRequest<Result<PayslipResponse>>;

public sealed record ListStaffCompensationQuery : IRequest<Result<IReadOnlyList<StaffCompensationResponse>>>;

public sealed class PayrollQueryHandler :
    IRequestHandler<ListPayrollRunsQuery, Result<IReadOnlyList<PayrollRunSummary>>>,
    IRequestHandler<GetPayrollRunQuery, Result<PayrollRunDetails>>,
    IRequestHandler<GetPayslipQuery, Result<PayslipResponse>>,
    IRequestHandler<ListStaffCompensationQuery, Result<IReadOnlyList<StaffCompensationResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPayrollStore _store;
    private readonly IPermissionService _permissions;

    public PayrollQueryHandler(IOperationGuard guard, ICurrentActor actor, IPayrollStore store, IPermissionService permissions)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _permissions = permissions;
    }

    public async Task<Result<IReadOnlyList<PayrollRunSummary>>> Handle(ListPayrollRunsQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenRead(cancellationToken).ConfigureAwait(false);
        return opened.IsFailure
            ? Result.Failure<IReadOnlyList<PayrollRunSummary>>(opened.Error)
            : Result.Success(await _store.ListRunsAsync(opened.Value, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<PayrollRunDetails>> Handle(GetPayrollRunQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure || _actor.UserId is not Guid userId)
        {
            return Result.Failure<PayrollRunDetails>(opened.IsFailure ? opened.Error : IdentityErrors.InvalidCredentials);
        }

        var run = await _store.GetRunAsync(opened.Value, request.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.NotFound);
        }

        var canApprove = await _permissions.HasPermissionAsync(userId, opened.Value, PermissionCodes.PayrollApprove, cancellationToken).ConfigureAwait(false);
        var canPay = await _permissions.HasPermissionAsync(userId, opened.Value, PermissionCodes.PayrollPay, cancellationToken).ConfigureAwait(false);
        var lines = await _store.ListLinesAsync(opened.Value, run.Id, cancellationToken).ConfigureAwait(false);
        var mapped = new List<PayrollLineItem>(lines.Count);
        foreach (var line in lines)
        {
            mapped.Add(PayrollMaps.ToLine(
                line,
                await _store.ListAdjustmentsAsync(opened.Value, line.Id, cancellationToken).ConfigureAwait(false),
                await _store.ListPaymentsAsync(opened.Value, line.Id, cancellationToken).ConfigureAwait(false)));
        }

        return Result.Success(PayrollMaps.ToDetails(run, mapped, canApprove, canPay));
    }

    public async Task<Result<PayslipResponse>> Handle(GetPayslipQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayslipResponse>(opened.Error);
        }

        var line = await _store.GetLineAsync(opened.Value, request.LineId, cancellationToken).ConfigureAwait(false);
        var run = line is null ? null : await _store.GetRunAsync(opened.Value, line.PayrollRunId, cancellationToken).ConfigureAwait(false);
        if (line is null || run is null)
        {
            return Result.Failure<PayslipResponse>(GymErrors.NotFound);
        }

        var adjustments = await _store.ListAdjustmentsAsync(opened.Value, line.Id, cancellationToken).ConfigureAwait(false);
        var payments = await _store.ListPaymentsAsync(opened.Value, line.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PayslipResponse(
            line.Id, run.Id, run.PeriodYear, run.PeriodMonth, line.EmployeeName, line.EmployeeCode, run.Currency,
            line.BaseSalary, line.Allowances, line.Commissions, line.Bonuses, line.Deductions, line.GrossPay, line.NetPay, line.AmountPaid, line.PaymentStatus.ToString(),
            adjustments.Select(PayrollMaps.ToAdjustment).ToArray(),
            payments.Select(PayrollMaps.ToPayment).ToArray()));
    }

    public async Task<Result<IReadOnlyList<StaffCompensationResponse>>> Handle(ListStaffCompensationQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<IReadOnlyList<StaffCompensationResponse>>(opened.Error);
        }

        var staff = await _store.ActiveStaffAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        var names = await _store.UserNamesAsync(opened.Value, staff.Select(item => item.UserId).ToArray(), cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = new List<StaffCompensationResponse>(staff.Count);
        foreach (var profile in staff)
        {
            var compensation = await _store.CurrentCompensationAsync(opened.Value, profile.Id, today, cancellationToken).ConfigureAwait(false);
            rows.Add(new StaffCompensationResponse(
                profile.Id,
                names.GetValueOrDefault(profile.UserId, profile.EmployeeCode),
                profile.EmployeeCode,
                compensation?.BaseSalary ?? 0m,
                compensation?.Allowances ?? 0m,
                compensation?.Currency ?? await _store.WorkspaceCurrencyAsync(opened.Value, cancellationToken).ConfigureAwait(false),
                compensation?.EffectiveFrom ?? today,
                compensation?.EffectiveTo,
                compensation?.Notes));
        }

        return Result.Success<IReadOnlyList<StaffCompensationResponse>>(rows);
    }

    private async Task<Result<Guid>> OpenRead(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.PayrollRead, FeatureCodes.GymStaffPayroll, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success(tenantId);
    }
}
