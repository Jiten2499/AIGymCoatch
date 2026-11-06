using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Payroll;

public sealed record AddPayrollAdjustmentCommand(Guid LineId, PayrollAdjustmentKind Kind, string Label, decimal Amount, string Reason) : IRequest<Result<PayrollLineItem>>;

public sealed class AddPayrollAdjustmentCommandValidator : AbstractValidator<AddPayrollAdjustmentCommand>
{
    public AddPayrollAdjustmentCommandValidator()
    {
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.Label).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Amount).GreaterThan(0m);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed record RemovePayrollAdjustmentCommand(Guid AdjustmentId) : IRequest<Result<PayrollLineItem>>;

public sealed record SaveStaffCompensationCommand(Guid StaffProfileId, decimal BaseSalary, decimal Allowances, DateOnly EffectiveFrom, string? Notes) : IRequest<Result<StaffCompensationResponse>>;

public sealed class SaveStaffCompensationCommandValidator : AbstractValidator<SaveStaffCompensationCommand>
{
    public SaveStaffCompensationCommandValidator()
    {
        RuleFor(command => command.StaffProfileId).NotEmpty();
        RuleFor(command => command.BaseSalary).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.Allowances).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.Notes).MaximumLength(500);
    }
}

public sealed class PayrollEditHandler :
    IRequestHandler<AddPayrollAdjustmentCommand, Result<PayrollLineItem>>,
    IRequestHandler<RemovePayrollAdjustmentCommand, Result<PayrollLineItem>>,
    IRequestHandler<SaveStaffCompensationCommand, Result<StaffCompensationResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPayrollStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public PayrollEditHandler(IOperationGuard guard, ICurrentActor actor, IPayrollStore store, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PayrollLineItem>> Handle(AddPayrollAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPrepare(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayrollLineItem>(opened.Error);
        }

        var line = await _store.GetLineAsync(opened.Value.TenantId, request.LineId, cancellationToken).ConfigureAwait(false);
        var run = line is null ? null : await _store.GetRunAsync(opened.Value.TenantId, line.PayrollRunId, cancellationToken).ConfigureAwait(false);
        if (line is null || run is null || !run.CanEdit || line.Locked)
        {
            return Result.Failure<PayrollLineItem>(line is null || run is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        var adjustment = PayrollAdjustment.Create(Guid.NewGuid(), opened.Value.TenantId, line.Id, request.Kind, request.Label, request.Amount, request.Reason, opened.Value.UserId);
        await _store.AddAdjustmentAsync(adjustment, cancellationToken).ConfigureAwait(false);
        await RefreshAdjustments(line, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), opened.Value.TenantId, opened.Value.UserId, "PayrollAdjustmentAdded", "PayrollAdjustment", adjustment.Id, null, request.Kind.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapLine(line, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<PayrollLineItem>> Handle(RemovePayrollAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPrepare(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayrollLineItem>(opened.Error);
        }

        var adjustment = await _store.GetAdjustmentAsync(opened.Value.TenantId, request.AdjustmentId, cancellationToken).ConfigureAwait(false);
        var line = adjustment is null ? null : await _store.GetLineAsync(opened.Value.TenantId, adjustment.PayrollLineId, cancellationToken).ConfigureAwait(false);
        var run = line is null ? null : await _store.GetRunAsync(opened.Value.TenantId, line.PayrollRunId, cancellationToken).ConfigureAwait(false);
        if (adjustment is null || line is null || run is null || !run.CanEdit || line.Locked)
        {
            return Result.Failure<PayrollLineItem>(adjustment is null || line is null || run is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        await _store.RemoveAdjustmentAsync(adjustment, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await RefreshAdjustments(line, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapLine(line, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<StaffCompensationResponse>> Handle(SaveStaffCompensationCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPrepare(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<StaffCompensationResponse>(opened.Error);
        }

        var staff = (await _store.ActiveStaffAsync(opened.Value.TenantId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(item => item.Id == request.StaffProfileId);
        if (staff is null)
        {
            return Result.Failure<StaffCompensationResponse>(GymErrors.NotFound);
        }

        var currency = await _store.WorkspaceCurrencyAsync(opened.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var current = await _store.TrackedCompensationAsync(opened.Value.TenantId, staff.Id, request.EffectiveFrom, cancellationToken).ConfigureAwait(false);
        if (current is not null && current.EffectiveFrom == request.EffectiveFrom)
        {
            current.Update(request.BaseSalary, request.Allowances, request.Notes);
        }
        else
        {
            if (current is not null)
            {
                current.Close(request.EffectiveFrom.AddDays(-1));
            }

            current = StaffCompensation.Create(Guid.NewGuid(), opened.Value.TenantId, staff.Id, request.BaseSalary, request.Allowances, currency, request.EffectiveFrom, request.Notes);
            await _store.AddCompensationAsync(current, cancellationToken).ConfigureAwait(false);
        }

        var names = await _store.UserNamesAsync(opened.Value.TenantId, [staff.UserId], cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), opened.Value.TenantId, opened.Value.UserId, "StaffCompensationSaved", "StaffCompensation", current.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new StaffCompensationResponse(staff.Id, names.GetValueOrDefault(staff.UserId, staff.EmployeeCode), staff.EmployeeCode, current.BaseSalary, current.Allowances, current.Currency, current.EffectiveFrom, current.EffectiveTo, current.Notes));
    }

    private async Task RefreshAdjustments(PayrollLine line, CancellationToken cancellationToken)
    {
        var adjustments = await _store.ListAdjustmentsAsync(line.TenantId, line.Id, cancellationToken).ConfigureAwait(false);
        line.SetAdjustments(
            adjustments.Where(item => item.Kind == PayrollAdjustmentKind.Bonus).Sum(item => item.Amount),
            adjustments.Where(item => item.Kind == PayrollAdjustmentKind.Deduction).Sum(item => item.Amount));
    }

    private async Task<PayrollLineItem> MapLine(PayrollLine line, CancellationToken cancellationToken) =>
        PayrollMaps.ToLine(
            line,
            await _store.ListAdjustmentsAsync(line.TenantId, line.Id, cancellationToken).ConfigureAwait(false),
            await _store.ListPaymentsAsync(line.TenantId, line.Id, cancellationToken).ConfigureAwait(false));

    private async Task<Result<(Guid TenantId, Guid UserId)>> OpenPrepare(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.PayrollPrepare, FeatureCodes.GymStaffPayroll, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, Guid)>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success((tenantId, userId));
    }
}
