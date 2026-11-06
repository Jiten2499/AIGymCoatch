using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Payroll;

public sealed record PayrollRunSummary(
    Guid Id,
    int PeriodYear,
    int PeriodMonth,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Currency,
    string Status,
    int StaffCount,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPayable,
    decimal AmountPaid,
    int IssueCount);

public sealed record PayrollAdjustmentItem(Guid Id, string Kind, string Label, decimal Amount, string Reason);

public sealed record PayrollPaymentItem(Guid Id, decimal Amount, DateOnly PaymentDate, string Method, string? Reference);

public sealed record PayrollLineItem(
    Guid Id,
    Guid StaffProfileId,
    Guid UserId,
    string EmployeeName,
    string EmployeeCode,
    string StaffType,
    decimal BaseSalary,
    decimal Allowances,
    decimal Commissions,
    decimal Bonuses,
    decimal Deductions,
    decimal GrossPay,
    decimal NetPay,
    decimal AmountPaid,
    decimal Remaining,
    string PaymentStatus,
    bool MissingSalary,
    bool UnapprovedCommission,
    bool UnconfirmedAttendance,
    bool Locked,
    IReadOnlyList<PayrollAdjustmentItem> Adjustments,
    IReadOnlyList<PayrollPaymentItem> Payments);

public sealed record PayrollRunDetails(
    Guid Id,
    int PeriodYear,
    int PeriodMonth,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Currency,
    string Status,
    int StaffCount,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPayable,
    decimal AmountPaid,
    int IssueCount,
    bool CanEdit,
    bool CanApprove,
    bool CanPay,
    IReadOnlyList<PayrollLineItem> Lines);

public sealed record StaffCompensationResponse(
    Guid StaffProfileId,
    string EmployeeName,
    string EmployeeCode,
    decimal BaseSalary,
    decimal Allowances,
    string Currency,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Notes);

public sealed record PayslipResponse(
    Guid PayrollLineId,
    Guid PayrollRunId,
    int PeriodYear,
    int PeriodMonth,
    string EmployeeName,
    string EmployeeCode,
    string Currency,
    decimal BaseSalary,
    decimal Allowances,
    decimal Commissions,
    decimal Bonuses,
    decimal Deductions,
    decimal GrossPay,
    decimal NetPay,
    decimal AmountPaid,
    string PaymentStatus,
    IReadOnlyList<PayrollAdjustmentItem> Adjustments,
    IReadOnlyList<PayrollPaymentItem> Payments);

public interface IPayrollStore
{
    Task<PayrollRun?> GetRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken);

    Task<PayrollRun?> FindRunAsync(Guid tenantId, int year, int month, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayrollRunSummary>> ListRunsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task AddRunAsync(PayrollRun run, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayrollLine>> ListLinesAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken);

    Task<PayrollLine?> GetLineAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken);

    Task AddLineAsync(PayrollLine line, CancellationToken cancellationToken);

    Task RemoveLinesAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken);

    Task RemoveLineAsync(PayrollLine line, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayrollAdjustment>> ListAdjustmentsAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken);

    Task AddAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken);

    Task<PayrollAdjustment?> GetAdjustmentAsync(Guid tenantId, Guid adjustmentId, CancellationToken cancellationToken);

    Task RemoveAdjustmentAsync(PayrollAdjustment adjustment, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayrollPayment>> ListPaymentsAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken);

    Task<bool> PaymentReferenceExistsAsync(Guid tenantId, Guid lineId, string reference, CancellationToken cancellationToken);

    Task AddPaymentAsync(PayrollPayment payment, CancellationToken cancellationToken);

    Task<StaffCompensation?> CurrentCompensationAsync(Guid tenantId, Guid staffProfileId, DateOnly onDay, CancellationToken cancellationToken);

    Task<StaffCompensation?> TrackedCompensationAsync(Guid tenantId, Guid staffProfileId, DateOnly onDay, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffCompensation>> ListCompensationAsync(Guid tenantId, CancellationToken cancellationToken);

    Task AddCompensationAsync(StaffCompensation compensation, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffProfile>> ActiveStaffAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Dictionary<Guid, string>> UserNamesAsync(Guid tenantId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken);

    Task<Dictionary<Guid, Guid>> TrainerIdsByUserAsync(Guid tenantId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken);

    Task<decimal> ApprovedCommissionsAsync(Guid tenantId, Guid trainerProfileId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<bool> HasUnapprovedCommissionAsync(Guid tenantId, Guid trainerProfileId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<string> WorkspaceCurrencyAsync(Guid tenantId, CancellationToken cancellationToken);
}
