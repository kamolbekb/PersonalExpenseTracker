using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<GoldPrice> GoldPrices => Set<GoldPrice>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.TelegramUserId).IsUnique();
        b.Entity<Setting>().HasIndex(s => s.UserId).IsUnique();
        b.Entity<Expense>().HasIndex(e => new { e.UserId, e.SpentOn });
        b.Entity<Category>().HasIndex(c => c.UserId);
        b.Entity<Budget>().HasIndex(bg => bg.UserId);

        b.Entity<Expense>().Property(e => e.Amount).HasColumnType("decimal(18,2)");
        b.Entity<Budget>().Property(e => e.LimitAmount).HasColumnType("decimal(18,2)");

        b.Entity<CurrencyRate>().HasIndex(r => new { r.Source, r.CurrencyCode, r.AsOfDate }).IsUnique();
        b.Entity<CurrencyRate>().HasIndex(r => r.AsOfDate);
        b.Entity<CurrencyRate>().Property(r => r.Rate).HasColumnType("decimal(18,6)");
        b.Entity<CurrencyRate>().Property(r => r.Buy).HasColumnType("decimal(18,6)");
        b.Entity<CurrencyRate>().Property(r => r.Sell).HasColumnType("decimal(18,6)");
        b.Entity<GoldPrice>().HasIndex(g => new { g.AsOfDate, g.Item }).IsUnique();
        b.Entity<GoldPrice>().Property(g => g.SellPrice).HasColumnType("decimal(18,2)");
        b.Entity<GoldPrice>().Property(g => g.BuyBackPrice).HasColumnType("decimal(18,2)");
    }
}
