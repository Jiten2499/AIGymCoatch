using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class StaffCompensation : BaseEntity
{
    private StaffCompensation()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid StaffProfileId { get; private set; }
    public decimal BaseSalary { get; private set; }
    public decimal Allowances { get; private set; }
    public string Currency { get; private set; } = "USD";
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? Notes { get; private set; }

    public static StaffCompensation Create(Guid id, Guid tenantId, Guid staffProfileId, decimal baseSalary, decimal allowances, string currency, DateOnly from, string? notes)
    {
        if (baseSalary < 0m || allowances < 0m || string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("Compensation amounts and currency are invalid.");
        }

        return new StaffCompensation
        {
            Id = id,
            TenantId = tenantId,
            StaffProfileId = staffProfileId,
            BaseSalary = Round(baseSalary),
            Allowances = Round(allowances),
            Currency = currency.Trim().ToUpperInvariant(),
            EffectiveFrom = from,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
    }

    public bool Covers(DateOnly day) => day >= EffectiveFrom && (EffectiveTo is null || day <= EffectiveTo);

    public void Close(DateOnly day) => EffectiveTo = day < EffectiveFrom ? EffectiveFrom : day;

    public void Update(decimal baseSalary, decimal allowances, string? notes)
    {
        if (baseSalary < 0m || allowances < 0m)
        {
            throw new ArgumentException("Compensation amounts are invalid.");
        }

        BaseSalary = Round(baseSalary);
        Allowances = Round(allowances);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
