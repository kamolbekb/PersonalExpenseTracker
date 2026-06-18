namespace ExpenseTracker.Application.Categories;

public record CategoryInput(string Name, string Emoji);
public record CategoryDto(int Id, string Name, string Emoji, bool IsArchived);
