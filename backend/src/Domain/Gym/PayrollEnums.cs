namespace LifeFitAI.Domain.Gym;

public enum PayrollRunStatus
{
    Draft = 1,
    InReview = 2,
    Approved = 3,
    PartiallyPaid = 4,
    Paid = 5,
}

public enum PayrollPaymentStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
}

public enum PayrollAdjustmentKind
{
    Bonus = 1,
    Deduction = 2,
}

public enum PayrollPaymentMethod
{
    Cash = 1,
    Upi = 2,
    BankTransfer = 3,
    Card = 4,
    Other = 5,
}

public static class PayrollMath
{
    public static decimal Gross(decimal baseSalary, decimal allowances, decimal commissions, decimal bonuses) =>
        Math.Round(baseSalary + allowances + commissions + bonuses, 2, MidpointRounding.AwayFromZero);

    public static decimal Net(decimal gross, decimal deductions) =>
        Math.Round(Math.Max(0m, gross - deductions), 2, MidpointRounding.AwayFromZero);
}
