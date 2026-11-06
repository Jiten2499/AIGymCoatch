using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class Expense : BaseEntity
{
    private Expense()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public DateOnly ExpenseDate { get; private set; }

    public ExpensePaymentMethod PaymentMethod { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public string? Notes { get; private set; }

    public ExpenseStatus Status { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public Guid? VoidedByUserId { get; private set; }

    public static Expense Create(
        Guid id,
        Guid tenantId,
        Guid categoryId,
        string description,
        decimal amount,
        string currency,
        DateOnly expenseDate,
        ExpensePaymentMethod paymentMethod,
        Guid recordedByUserId,
        string? notes)
    {
        return new Expense
        {
            Id = id,
            TenantId = tenantId,
            CategoryId = categoryId,
            Description = description.Trim(),
            Amount = amount,
            Currency = currency,
            ExpenseDate = expenseDate,
            PaymentMethod = paymentMethod,
            RecordedByUserId = recordedByUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = ExpenseStatus.Active,
        };
    }

    public bool Update(Guid categoryId, string description, decimal amount, DateOnly expenseDate, ExpensePaymentMethod paymentMethod, string? notes)
    {
        if (Status != ExpenseStatus.Active || !ExpenseRules.Amount(amount))
        {
            return false;
        }

        CategoryId = categoryId;
        Description = description.Trim();
        Amount = amount;
        ExpenseDate = expenseDate;
        PaymentMethod = paymentMethod;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return true;
    }

    public bool Void(Guid userId, DateTime utcNow)
    {
        if (Status != ExpenseStatus.Active)
        {
            return false;
        }

        Status = ExpenseStatus.Voided;
        VoidedAt = utcNow;
        VoidedByUserId = userId;
        return true;
    }
}
