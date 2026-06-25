namespace ExpenseTracker.Domain;

public static class DefaultIncomeCategories
{
    public static readonly IReadOnlyList<(string Name, string Emoji)> All = new[]
    {
        ("Salary", "💼"), ("Freelance", "💻"), ("Gifts", "🎁"),
        ("Investments", "📈"), ("Other", "💰"),
    };
}
