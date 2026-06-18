namespace ExpenseTracker.Domain;

public static class DefaultCategories
{
    public static readonly IReadOnlyList<(string Name, string Emoji)> All = new[]
    {
        ("Food", "🍔"), ("Transport", "🚌"), ("Rent", "🏠"),
        ("Groceries", "🛒"), ("Entertainment", "🎬"), ("Health", "💊"),
        ("Shopping", "🛍️"), ("Other", "📦"),
    };
}
