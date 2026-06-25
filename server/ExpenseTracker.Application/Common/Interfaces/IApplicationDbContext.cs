using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<CurrencyRate> CurrencyRates { get; }
    DbSet<GoldPrice> GoldPrices { get; }
    DbSet<Setting> Settings { get; }
    DbSet<Income> Incomes { get; }
    DbSet<IncomeCategory> IncomeCategories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
