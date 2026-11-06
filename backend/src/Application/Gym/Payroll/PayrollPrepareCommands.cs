using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Payroll;

public sealed record PreparePayrollCommand(int Year, int Month) : IRequest<Result<PayrollRunDetails>>;

public sealed class PreparePayrollCommandValidator : AbstractValidator<PreparePayrollCommand>
{
    public PreparePayrollCommandValidator()
    {
        RuleFor(command => command.Year).InclusiveBetween(2000, 2100);
        RuleFor(command => command.Month).InclusiveBetween(1, 12);
    }
}

public sealed record SubmitPayrollReviewCommand(Guid RunId) : IRequest<Result<PayrollRunDetails>>;

public sealed record ReturnPayrollToDraftCommand(Guid RunId) : IRequest<Result<PayrollRunDetails>>;

public sealed class PayrollPrepareHandler :
    IRequestHandler<PreparePayrollCommand, Result<PayrollRunDetails>>,
    IRequestHandler<SubmitPayrollReviewCommand, Result<PayrollRunDetails>>,
    IRequestHandler<ReturnPayrollToDraftCommand, Result<PayrollRunDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPayrollStore _store;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public PayrollPrepareHandler(IOperationGuard guard, ICurrentActor actor, IPayrollStore store, IPermissionService permissions, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _permissions = permissions;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PayrollRunDetails>> Handle(PreparePayrollCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPrepare(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayrollRunDetails>(opened.Error);
        }

        var (tenantId, userId) = opened.Value;
        var existing = await _store.FindRunAsync(tenantId, request.Year, request.Month, cancellationToken).ConfigureAwait(false);
        if (existing is not null && !existing.CanEdit)
        {
            return await Details(existing, cancellationToken).ConfigureAwait(false);
        }

        var currency = await _store.WorkspaceCurrencyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var run = existing ?? PayrollRun.Create(Guid.NewGuid(), tenantId, request.Year, request.Month, currency, userId, _clock.UtcNow);
        if (existing is null)
        {
            await _store.AddRunAsync(run, cancellationToken).ConfigureAwait(false);
        }

        await SyncLines(run, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "PayrollPrepared", "PayrollRun", run.Id, null, $"{run.PeriodYear}-{run.PeriodMonth:00}", _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await Details(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PayrollRunDetails>> Handle(SubmitPayrollReviewCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPrepare(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayrollRunDetails>(opened.Error);
        }

        var run = await _store.GetRunAsync(opened.Value.TenantId, request.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.NotFound);
        }

        var lines = await _store.ListLinesAsync(opened.Value.TenantId, run.Id, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0 || lines.Any(line => line.MissingSalary || line.UnapprovedCommission))
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.InvalidState);
        }

        if (!run.Submit(_clock.UtcNow))
        {
            return Result.Failure<PayrollRunDetails>(GymErrors.InvalidState);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), opened.Value.TenantId, opened.Value.UserId, "PayrollSubmitted", "PayrollRun", run.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await Details(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PayrollRunDetails>> Handle(ReturnPayrollToDraftCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenPrepare(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<PayrollRunDetails>(opened.Error);
        }

        var run = await _store.GetRunAsync(opened.Value.TenantId, request.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null || !run.ReturnToDraft())
        {
            return Result.Failure<PayrollRunDetails>(run is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await Details(run, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncLines(PayrollRun run, CancellationToken cancellationToken)
    {
        if (!run.CanEdit)
        {
            return;
        }

        var existing = (await _store.ListLinesAsync(run.TenantId, run.Id, cancellationToken).ConfigureAwait(false)).ToDictionary(line => line.StaffProfileId);
        var staff = await _store.ActiveStaffAsync(run.TenantId, cancellationToken).ConfigureAwait(false);
        var names = await _store.UserNamesAsync(run.TenantId, staff.Select(item => item.UserId).ToArray(), cancellationToken).ConfigureAwait(false);
        var trainers = await _store.TrainerIdsByUserAsync(run.TenantId, staff.Select(item => item.UserId).ToArray(), cancellationToken).ConfigureAwait(false);
        var keep = new HashSet<Guid>();
        foreach (var profile in staff)
        {
            keep.Add(profile.Id);
            var compensation = await _store.CurrentCompensationAsync(run.TenantId, profile.Id, run.PeriodEnd, cancellationToken).ConfigureAwait(false);
            var missing = compensation is null;
            var baseSalary = compensation?.BaseSalary ?? 0m;
            var allowances = compensation?.Allowances ?? 0m;
            var commissions = 0m;
            var unapproved = false;
            if (trainers.TryGetValue(profile.UserId, out var trainerId))
            {
                commissions = await _store.ApprovedCommissionsAsync(run.TenantId, trainerId, run.PeriodStart, run.PeriodEnd, cancellationToken).ConfigureAwait(false);
                unapproved = await _store.HasUnapprovedCommissionAsync(run.TenantId, trainerId, run.PeriodStart, run.PeriodEnd, cancellationToken).ConfigureAwait(false);
            }

            if (existing.TryGetValue(profile.Id, out var line))
            {
                line.ReplaceEarnings(baseSalary, allowances, commissions, missing, unapproved, false);
                continue;
            }

            await _store.AddLineAsync(
                PayrollLine.Create(
                    Guid.NewGuid(), run.TenantId, run.Id, profile.Id, profile.UserId,
                    names.GetValueOrDefault(profile.UserId, profile.EmployeeCode),
                    profile.EmployeeCode, profile.StaffType.ToString(),
                    baseSalary, allowances, commissions, missing, unapproved, false),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var orphan in existing.Values.Where(line => !keep.Contains(line.StaffProfileId)))
        {
            await _store.RemoveLineAsync(orphan, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result<(Guid TenantId, Guid UserId)>> OpenPrepare(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.PayrollPrepare, FeatureCodes.GymStaffPayroll, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, Guid)>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success((tenantId, userId));
    }

    private async Task<Result<PayrollRunDetails>> Details(PayrollRun run, CancellationToken cancellationToken)
    {
        var canApprove = _actor.UserId is Guid userId
            && await _permissions.HasPermissionAsync(userId, run.TenantId, PermissionCodes.PayrollApprove, cancellationToken).ConfigureAwait(false);
        var canPay = _actor.UserId is Guid payUser
            && await _permissions.HasPermissionAsync(payUser, run.TenantId, PermissionCodes.PayrollPay, cancellationToken).ConfigureAwait(false);
        var lines = await _store.ListLinesAsync(run.TenantId, run.Id, cancellationToken).ConfigureAwait(false);
        var mapped = new List<PayrollLineItem>(lines.Count);
        foreach (var line in lines)
        {
            var adjustments = await _store.ListAdjustmentsAsync(run.TenantId, line.Id, cancellationToken).ConfigureAwait(false);
            var payments = await _store.ListPaymentsAsync(run.TenantId, line.Id, cancellationToken).ConfigureAwait(false);
            mapped.Add(PayrollMaps.ToLine(line, adjustments, payments));
        }

        return Result.Success(PayrollMaps.ToDetails(run, mapped, canApprove, canPay));
    }
}
