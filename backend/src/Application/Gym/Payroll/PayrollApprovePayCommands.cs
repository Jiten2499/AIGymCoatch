using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Payroll;

public sealed record ApprovePayrollCommand(Guid RunId, bool Confirm) : IRequest<Result<PayrollRunDetails>>;

public sealed record RecordPayrollPaymentCommand(Guid LineId, decimal Amount, DateOnly PaymentDate, PayrollPaymentMethod Method, string? Reference) : IRequest<Result<PayrollLineItem>>;

public sealed class RecordPayrollPaymentCommandValidator : AbstractValidator<RecordPayrollPaymentCommand>
{
    public RecordPayrollPaymentCommandValidator()
    {
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0m);
        RuleFor(command => command.Reference).MaximumLength(100);
    }
}

public sealed record RecordBulkPayrollPaymentsCommand(Guid RunId, DateOnly PaymentDate, PayrollPaymentMethod Method, string? ReferencePrefix) : IRequest<Result<PayrollRunDetails>>;

public sealed class PayrollApprovePayHandler :
    IRequestHandler<ApprovePayrollCommand, Result<PayrollRunDetails>>,
    IRequestHandler<RecordPayrollPaymentCommand, Result<PayrollLineItem>>,
    IRequestHandler<RecordBulkPayrollPaymentsCommand, Result<PayrollRunDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPayrollStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public PayrollApprovePayHandler(IOperationGuard guard, ICurrentActor actor, IPayrollStore store, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PayrollRunDetails>> Handle(ApprovePayrollCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.PayrollApprove, FeatureCodes.GymStaffPayroll, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<PayrollRunDetails>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        if (!request.Confirm)
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.InvalidState);
        }

        var run = await _store.GetRunAsync(tenantId, request.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.NotFound);
        }

        var lines = await _store.ListLinesAsync(tenantId, run.Id, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0 || lines.Any(line => line.MissingSalary || line.UnapprovedCommission))
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.InvalidState);
        }

        if (!run.Approve(userId, _clock.UtcNow))
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.InvalidState);
        }

        foreach (var line in lines)
        {
            line.Lock();
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "PayrollApproved", "PayrollRun", run.Id, null, $"{lines.Count} staff", _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await Details(run, true, true, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<PayrollLineItem>> Handle(RecordPayrollPaymentCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPay(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayrollLineItem>(opened.Error);
        }

        var line = await _store.GetLineAsync(opened.Value.TenantId, request.LineId, cancellationToken).ConfigureAwait(false);
        var run = line is null ? null : await _store.GetRunAsync(opened.Value.TenantId, line.PayrollRunId, cancellationToken).ConfigureAwait(false);
        if (line is null || run is null || !line.Locked)
        {
            return Result.Failure<PayrollLineItem>(line is null || run is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        if (!string.IsNullOrWhiteSpace(request.Reference)
            && await _store.PaymentReferenceExistsAsync(opened.Value.TenantId, line.Id, request.Reference.Trim(), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PayrollLineItem>(GymErrors.Conflict);
        }

        if (!line.ApplyPayment(request.Amount))
        {
            return Result.Failure<PayrollLineItem>(GymErrors.InvalidState);
        }

        var payment = PayrollPayment.Create(Guid.NewGuid(), opened.Value.TenantId, run.Id, line.Id, request.Amount, request.PaymentDate, request.Method, request.Reference, opened.Value.UserId);
        await _store.AddPaymentAsync(payment, cancellationToken).ConfigureAwait(false);
        await RefreshRunPayments(run, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), opened.Value.TenantId, opened.Value.UserId, "PayrollPaymentRecorded", "PayrollPayment", payment.Id, null, payment.Amount.ToString("0.00"), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(PayrollMaps.ToLine(line, await _store.ListAdjustmentsAsync(line.TenantId, line.Id, cancellationToken).ConfigureAwait(false), await _store.ListPaymentsAsync(line.TenantId, line.Id, cancellationToken).ConfigureAwait(false)));
    }

    public async Task<Result<PayrollRunDetails>> Handle(RecordBulkPayrollPaymentsCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPay(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayrollRunDetails>(opened.Error);
        }

        var run = await _store.GetRunAsync(opened.Value.TenantId, request.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null || run.Status is PayrollRunStatus.Draft or PayrollRunStatus.InReview)
        {
            return Result.Failure<PayrollRunDetails>(run is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        var lines = await _store.ListLinesAsync(opened.Value.TenantId, run.Id, cancellationToken).ConfigureAwait(false);
        foreach (var line in lines.Where(item => item.Remaining > 0m))
        {
            var reference = string.IsNullOrWhiteSpace(request.ReferencePrefix) ? null : $"{request.ReferencePrefix.Trim()}-{line.EmployeeCode}";
            if (reference is not null && await _store.PaymentReferenceExistsAsync(opened.Value.TenantId, line.Id, reference, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var amount = line.Remaining;
            if (!line.ApplyPayment(amount))
            {
                continue;
            }

            await _store.AddPaymentAsync(PayrollPayment.Create(Guid.NewGuid(), opened.Value.TenantId, run.Id, line.Id, amount, request.PaymentDate, request.Method, reference, opened.Value.UserId), cancellationToken).ConfigureAwait(false);
        }

        await RefreshRunPayments(run, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), opened.Value.TenantId, opened.Value.UserId, "PayrollBulkPaymentRecorded", "PayrollRun", run.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await Details(run, true, true, cancellationToken).ConfigureAwait(false));
    }

    private async Task RefreshRunPayments(PayrollRun run, CancellationToken cancellationToken)
    {
        var lines = await _store.ListLinesAsync(run.TenantId, run.Id, cancellationToken).ConfigureAwait(false);
        run.RefreshPaymentStatus(lines.Any(line => line.AmountPaid > 0m), lines.Count > 0 && lines.All(line => line.PaymentStatus == PayrollPaymentStatus.Paid));
    }

    private async Task<Result<(Guid TenantId, Guid UserId)>> OpenPay(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.PayrollPay, FeatureCodes.GymStaffPayroll, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, Guid)>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success((tenantId, userId));
    }

    private async Task<PayrollRunDetails> Details(PayrollRun run, bool canApprove, bool canPay, CancellationToken cancellationToken)
    {
        var lines = await _store.ListLinesAsync(run.TenantId, run.Id, cancellationToken).ConfigureAwait(false);
        var mapped = new List<PayrollLineItem>(lines.Count);
        foreach (var line in lines)
        {
            mapped.Add(PayrollMaps.ToLine(
                line,
                await _store.ListAdjustmentsAsync(run.TenantId, line.Id, cancellationToken).ConfigureAwait(false),
                await _store.ListPaymentsAsync(run.TenantId, line.Id, cancellationToken).ConfigureAwait(false)));
        }

        return PayrollMaps.ToDetails(run, mapped, canApprove, canPay);
    }
}
