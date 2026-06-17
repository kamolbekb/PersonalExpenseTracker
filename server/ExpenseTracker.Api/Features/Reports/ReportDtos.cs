namespace ExpenseTracker.Api.Features.Reports;

public record CategoryTotal(int CategoryId, string CategoryName, decimal Total);
public record MonthTotal(string Month, decimal Total);
public record ReportSummary(string BaseCurrency, decimal GrandTotal,
    IReadOnlyList<CategoryTotal> ByCategory, IReadOnlyList<MonthTotal> ByMonth);
