using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.TelegramUserId).IsUnique();
        b.Entity<Setting>().HasIndex(s => s.UserId).IsUnique();
        b.Entity<Expense>().HasIndex(e => new { e.UserId, e.SpentOn });
        b.Entity<Category>().HasIndex(c => c.UserId);
        b.Entity<Budget>().HasIndex(bg => bg.UserId);
        b.Entity<ExchangeRate>()
            .HasIndex(r => new { r.BaseCurrency, r.QuoteCurrency, r.AsOfDate }).IsUnique();

        b.Entity<Expense>().Property(e => e.Amount).HasColumnType("decimal(18,2)");
        b.Entity<Budget>().Property(e => e.LimitAmount).HasColumnType("decimal(18,2)");
        b.Entity<ExchangeRate>().Property(e => e.Rate).HasColumnType("decimal(18,8)");
    }
}
