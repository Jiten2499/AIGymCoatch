using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum FinancialAccountKind
{
    Cash = 1,
    Bank = 2,
    Upi = 3,
    Card = 4,
    Other = 5,
}

public enum LedgerAccountClass
{
    Asset = 1,
    Liability = 2,
    Revenue = 3,
    Expense = 4,
    Equity = 5,
}

public enum JournalEntryType
{
    MoneyIn = 1,
    MoneyOut = 2,
    Transfer = 3,
}

public enum JournalSourceType
{
    MembershipPayment = 1,
    Expense = 2,
    Transfer = 3,
    Reversal = 4,
}

public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Voided = 3,
}

public static class LedgerAccountCodes
{
    public const string MembershipRevenue = "REV_MEMBERSHIP";
    public const string OperatingExpense = "EXP_OPERATING";
    public static string AssetFor(Guid financialAccountId) => $"AST_{financialAccountId:N}";
}

public sealed class LedgerAccount : BaseEntity
{
    private LedgerAccount()
    {
    }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public LedgerAccountClass AccountClass { get; private set; }

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public static LedgerAccount Create(Guid id, Guid tenantId, string code, string name, LedgerAccountClass accountClass, bool isSystem) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            AccountClass = accountClass,
            IsSystem = isSystem,
            IsActive = true,
        };

    public void Rename(string name) => Name = name.Trim();

    public void SetActive(bool active) => IsActive = active;
}

public sealed class FinancialAccount : BaseEntity
{
    private FinancialAccount()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public FinancialAccountKind AccountKind { get; private set; }

    public string Currency { get; private set; } = "USD";

    public bool IsActive { get; private set; }

    public Guid LedgerAccountId { get; private set; }

    public static FinancialAccount Create(
        Guid id,
        Guid tenantId,
        string name,
        FinancialAccountKind accountKind,
        string currency,
        Guid ledgerAccountId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            AccountKind = accountKind,
            Currency = currency.ToUpperInvariant(),
            IsActive = true,
            LedgerAccountId = ledgerAccountId,
        };

    public void Update(string name, FinancialAccountKind accountKind)
    {
        Name = name.Trim();
        AccountKind = accountKind;
    }

    public void SetActive(bool active) => IsActive = active;
}

public sealed class JournalEntry : BaseEntity
{
    private JournalEntry()
    {
    }

    public Guid TenantId { get; private set; }

    public DateOnly EntryDate { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public JournalEntryType EntryType { get; private set; }

    public JournalSourceType SourceType { get; private set; }

    public Guid? SourceId { get; private set; }

    public string? Category { get; private set; }

    public Guid? FinancialAccountId { get; private set; }

    public Guid? CounterFinancialAccountId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public JournalEntryStatus Status { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public Guid? ReversalOfJournalEntryId { get; private set; }

    public DateTime? PostedAt { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public Guid? VoidedByUserId { get; private set; }

    public static (JournalEntry Entry, IReadOnlyList<JournalEntryLine> Lines) CreatePosted(
        Guid id,
        Guid tenantId,
        DateOnly entryDate,
        string description,
        JournalEntryType entryType,
        JournalSourceType sourceType,
        Guid? sourceId,
        string? category,
        Guid? financialAccountId,
        Guid? counterFinancialAccountId,
        decimal amount,
        string currency,
        Guid recordedByUserId,
        DateTime utcNow,
        Guid? reversalOfJournalEntryId,
        IReadOnlyList<(Guid LedgerAccountId, decimal Debit, decimal Credit)> lines)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (lines.Count < 2)
        {
            throw new ArgumentException("A journal entry requires at least two lines.", nameof(lines));
        }

        var debit = lines.Sum(line => line.Debit);
        var credit = lines.Sum(line => line.Credit);
        if (debit != credit || debit != amount)
        {
            throw new InvalidOperationException("Journal entry debits and credits must balance to the entry amount.");
        }

        var entry = new JournalEntry
        {
            Id = id,
            TenantId = tenantId,
            EntryDate = entryDate,
            Description = description.Trim(),
            EntryType = entryType,
            SourceType = sourceType,
            SourceId = sourceId,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            FinancialAccountId = financialAccountId,
            CounterFinancialAccountId = counterFinancialAccountId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            Status = JournalEntryStatus.Posted,
            RecordedByUserId = recordedByUserId,
            ReversalOfJournalEntryId = reversalOfJournalEntryId,
            PostedAt = utcNow,
        };

        var createdLines = lines
            .Select(line => JournalEntryLine.Create(Guid.NewGuid(), tenantId, id, line.LedgerAccountId, line.Debit, line.Credit))
            .ToList();
        return (entry, createdLines);
    }

    public bool Void(Guid userId, DateTime utcNow)
    {
        if (Status != JournalEntryStatus.Posted)
        {
            return false;
        }

        Status = JournalEntryStatus.Voided;
        VoidedAt = utcNow;
        VoidedByUserId = userId;
        return true;
    }
}

public sealed class JournalEntryLine : BaseEntity
{
    private JournalEntryLine()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid JournalEntryId { get; private set; }

    public Guid LedgerAccountId { get; private set; }

    public decimal DebitAmount { get; private set; }

    public decimal CreditAmount { get; private set; }

    public static JournalEntryLine Create(Guid id, Guid tenantId, Guid journalEntryId, Guid ledgerAccountId, decimal debit, decimal credit)
    {
        if (debit < 0 || credit < 0 || (debit == 0 && credit == 0) || (debit > 0 && credit > 0))
        {
            throw new ArgumentException("Each journal line must be either a debit or a credit.");
        }

        return new JournalEntryLine
        {
            Id = id,
            TenantId = tenantId,
            JournalEntryId = journalEntryId,
            LedgerAccountId = ledgerAccountId,
            DebitAmount = debit,
            CreditAmount = credit,
        };
    }
}

public static class LedgerMethodMap
{
    public static FinancialAccountKind FromMembershipMethod(MembershipPaymentMethod method) =>
        method switch
        {
            MembershipPaymentMethod.Cash => FinancialAccountKind.Cash,
            MembershipPaymentMethod.Upi => FinancialAccountKind.Upi,
            MembershipPaymentMethod.DebitCard or MembershipPaymentMethod.CreditCard => FinancialAccountKind.Card,
            MembershipPaymentMethod.BankTransfer or MembershipPaymentMethod.OnlinePayment or MembershipPaymentMethod.Cheque => FinancialAccountKind.Bank,
            _ => FinancialAccountKind.Other,
        };

    public static FinancialAccountKind FromExpenseMethod(ExpensePaymentMethod method) =>
        method switch
        {
            ExpensePaymentMethod.Cash => FinancialAccountKind.Cash,
            ExpensePaymentMethod.Upi => FinancialAccountKind.Upi,
            ExpensePaymentMethod.Card => FinancialAccountKind.Card,
            ExpensePaymentMethod.BankTransfer => FinancialAccountKind.Bank,
            _ => FinancialAccountKind.Other,
        };
}
