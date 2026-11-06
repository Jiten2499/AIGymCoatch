using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Payroll;

internal static class PayrollMaps
{
    public static PayrollAdjustmentItem ToAdjustment(PayrollAdjustment item) =>
        new(item.Id, item.Kind.ToString(), item.Label, item.Amount, item.Reason);

    public static PayrollPaymentItem ToPayment(PayrollPayment item) =>
        new(item.Id, item.Amount, item.PaymentDate, item.Method.ToString(), item.Reference);

    public static PayrollLineItem ToLine(PayrollLine line, IReadOnlyList<PayrollAdjustment> adjustments, IReadOnlyList<PayrollPayment> payments) =>
        new(
            line.Id,
            line.StaffProfileId,
            line.UserId,
            line.EmployeeName,
            line.EmployeeCode,
            line.StaffType,
            line.BaseSalary,
            line.Allowances,
            line.Commissions,
            line.Bonuses,
            line.Deductions,
            line.GrossPay,
            line.NetPay,
            line.AmountPaid,
            line.Remaining,
            line.PaymentStatus.ToString(),
            line.MissingSalary,
            line.UnapprovedCommission,
            line.UnconfirmedAttendance,
            line.Locked,
            adjustments.Select(ToAdjustment).ToArray(),
            payments.Select(ToPayment).ToArray());

    public static PayrollRunDetails ToDetails(PayrollRun run, IReadOnlyList<PayrollLineItem> lines, bool canApprove, bool canPay)
    {
        var issues = lines.Count(line => line.MissingSalary || line.UnapprovedCommission || line.UnconfirmedAttendance);
        return new PayrollRunDetails(
            run.Id,
            run.PeriodYear,
            run.PeriodMonth,
            run.PeriodStart,
            run.PeriodEnd,
            run.Currency,
            run.Status.ToString(),
            lines.Count,
            lines.Sum(line => line.GrossPay),
            lines.Sum(line => line.Deductions),
            lines.Sum(line => line.NetPay),
            lines.Sum(line => line.AmountPaid),
            issues,
            run.CanEdit,
            canApprove && run.Status is PayrollRunStatus.Draft or PayrollRunStatus.InReview,
            canPay && run.Status is PayrollRunStatus.Approved or PayrollRunStatus.PartiallyPaid or PayrollRunStatus.Paid,
            lines);
    }
}
