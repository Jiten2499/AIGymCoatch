using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class PayrollRun : BaseEntity
{
    private PayrollRun()
    {
    }

    public Guid TenantId { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public string Currency { get; private set; } = "USD";
    public PayrollRunStatus Status { get; private set; }
    public Guid? PreparedByUserId { get; private set; }
    public DateTime? PreparedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? Notes { get; private set; }

    public static PayrollRun Create(Guid id, Guid tenantId, int year, int month, string currency, Guid preparedBy, DateTime utcNow)
    {
        var start = new DateOnly(year, month, 1);
        return new PayrollRun
        {
            Id = id,
            TenantId = tenantId,
            PeriodYear = year,
            PeriodMonth = month,
            PeriodStart = start,
            PeriodEnd = start.AddMonths(1).AddDays(-1),
            Currency = currency.Trim().ToUpperInvariant(),
            Status = PayrollRunStatus.Draft,
            PreparedByUserId = preparedBy,
            PreparedAt = utcNow,
        };
    }

    public bool CanEdit => Status is PayrollRunStatus.Draft or PayrollRunStatus.InReview;

    public bool Submit(DateTime utcNow)
    {
        if (Status != PayrollRunStatus.Draft)
        {
            return false;
        }

        Status = PayrollRunStatus.InReview;
        SubmittedAt = utcNow;
        return true;
    }

    public bool ReturnToDraft()
    {
        if (Status != PayrollRunStatus.InReview)
        {
            return false;
        }

        Status = PayrollRunStatus.Draft;
        SubmittedAt = null;
        return true;
    }

    public bool Approve(Guid userId, DateTime utcNow)
    {
        if (Status is not (PayrollRunStatus.Draft or PayrollRunStatus.InReview))
        {
            return false;
        }

        Status = PayrollRunStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAt = utcNow;
        return true;
    }

    public void RefreshPaymentStatus(bool anyPaid, bool allPaid)
    {
        if (Status is PayrollRunStatus.Draft or PayrollRunStatus.InReview)
        {
            return;
        }

        Status = allPaid ? PayrollRunStatus.Paid : anyPaid ? PayrollRunStatus.PartiallyPaid : PayrollRunStatus.Approved;
    }
}

public sealed class PayrollLine : BaseEntity
{
    private PayrollLine()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid PayrollRunId { get; private set; }
    public Guid StaffProfileId { get; private set; }
    public Guid UserId { get; private set; }
    public string EmployeeName { get; private set; } = string.Empty;
    public string EmployeeCode { get; private set; } = string.Empty;
    public string StaffType { get; private set; } = string.Empty;
    public decimal BaseSalary { get; private set; }
    public decimal Allowances { get; private set; }
    public decimal Commissions { get; private set; }
    public decimal Bonuses { get; private set; }
    public decimal Deductions { get; private set; }
    public decimal GrossPay { get; private set; }
    public decimal NetPay { get; private set; }
    public decimal AmountPaid { get; private set; }
    public PayrollPaymentStatus PaymentStatus { get; private set; }
    public bool MissingSalary { get; private set; }
    public bool UnapprovedCommission { get; private set; }
    public bool UnconfirmedAttendance { get; private set; }
    public bool Locked { get; private set; }

    public static PayrollLine Create(
        Guid id, Guid tenantId, Guid runId, Guid staffId, Guid userId, string name, string code, string staffType,
        decimal baseSalary, decimal allowances, decimal commissions, bool missingSalary, bool unapprovedCommission, bool unconfirmedAttendance)
    {
        var line = new PayrollLine
        {
            Id = id,
            TenantId = tenantId,
            PayrollRunId = runId,
            StaffProfileId = staffId,
            UserId = userId,
            EmployeeName = name.Trim(),
            EmployeeCode = code.Trim(),
            StaffType = staffType,
            BaseSalary = Round(baseSalary),
            Allowances = Round(allowances),
            Commissions = Round(commissions),
            PaymentStatus = PayrollPaymentStatus.Pending,
            MissingSalary = missingSalary,
            UnapprovedCommission = unapprovedCommission,
            UnconfirmedAttendance = unconfirmedAttendance,
        };
        line.Recalculate();
        return line;
    }

    public void ReplaceEarnings(decimal baseSalary, decimal allowances, decimal commissions, bool missingSalary, bool unapprovedCommission, bool unconfirmedAttendance)
    {
        EnsureEditable();
        BaseSalary = Round(baseSalary);
        Allowances = Round(allowances);
        Commissions = Round(commissions);
        MissingSalary = missingSalary;
        UnapprovedCommission = unapprovedCommission;
        UnconfirmedAttendance = unconfirmedAttendance;
        Recalculate();
    }

    public void SetAdjustments(decimal bonuses, decimal deductions)
    {
        EnsureEditable();
        Bonuses = Round(Math.Max(0m, bonuses));
        Deductions = Round(Math.Max(0m, deductions));
        Recalculate();
    }

    public void Lock() { Locked = true; Recalculate(); }

    public decimal Remaining => Math.Max(0m, NetPay - AmountPaid);

    public bool ApplyPayment(decimal amount)
    {
        if (!Locked || amount <= 0m || amount > Remaining + 0.0001m)
        {
            return false;
        }

        AmountPaid = Round(AmountPaid + amount);
        PaymentStatus = AmountPaid <= 0m
            ? PayrollPaymentStatus.Pending
            : AmountPaid + 0.0001m >= NetPay ? PayrollPaymentStatus.Paid : PayrollPaymentStatus.PartiallyPaid;
        return true;
    }

    private void EnsureEditable()
    {
        if (Locked)
        {
            throw new InvalidOperationException("Approved payroll lines cannot be rewritten.");
        }
    }

    private void Recalculate()
    {
        GrossPay = PayrollMath.Gross(BaseSalary, Allowances, Commissions, Bonuses);
        NetPay = PayrollMath.Net(GrossPay, Deductions);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class PayrollAdjustment : BaseEntity
{
    private PayrollAdjustment()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public PayrollAdjustmentKind Kind { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }

    public static PayrollAdjustment Create(Guid id, Guid tenantId, Guid lineId, PayrollAdjustmentKind kind, string label, decimal amount, string reason, Guid userId)
    {
        if (amount <= 0m || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Adjustment requires a positive amount, label, and reason.");
        }

        return new PayrollAdjustment
        {
            Id = id,
            TenantId = tenantId,
            PayrollLineId = lineId,
            Kind = kind,
            Label = label.Trim(),
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            Reason = reason.Trim(),
            CreatedByUserId = userId,
        };
    }
}

public sealed class PayrollPayment : BaseEntity
{
    private PayrollPayment()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid PayrollRunId { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public PayrollPaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public Guid RecordedByUserId { get; private set; }

    public static PayrollPayment Create(Guid id, Guid tenantId, Guid runId, Guid lineId, decimal amount, DateOnly date, PayrollPaymentMethod method, string? reference, Guid userId)
    {
        if (amount <= 0m)
        {
            throw new ArgumentException("Payment amount must be positive.");
        }

        return new PayrollPayment
        {
            Id = id,
            TenantId = tenantId,
            PayrollRunId = runId,
            PayrollLineId = lineId,
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            PaymentDate = date,
            Method = method,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            RecordedByUserId = userId,
        };
    }
}
