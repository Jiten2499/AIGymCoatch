namespace LifeFitAI.Domain.Gym;

public enum ExpenseStatus
{
    Active = 1,
    Voided = 2,
}

public enum ExpensePaymentMethod
{
    Cash = 1,
    Upi = 2,
    BankTransfer = 3,
    Card = 4,
    Other = 5,
}

public static class ExpenseRules
{
    public static readonly string[] DefaultCategories =
    [
        "Rent",
        "Electricity",
        "Internet",
        "Salaries",
        "Cleaning",
        "Maintenance",
        "Equipment",
        "Marketing",
        "Music/License",
        "Other",
    ];

    public static bool Amount(decimal amount) => amount > 0 && amount <= 999_999_999.9999m;

    public static bool Date(DateOnly expenseDate, DateOnly localToday) =>
        expenseDate >= new DateOnly(2000, 1, 1) && expenseDate <= localToday.AddDays(7);
}
