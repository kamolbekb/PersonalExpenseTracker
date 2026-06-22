using ExpenseTracker.Application.Budgets;
using ExpenseTracker.Application.Categories;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.ExchangeRates;
using ExpenseTracker.Application.Expenses;
using ExpenseTracker.Application.Gold;
using ExpenseTracker.Application.Rates;
using ExpenseTracker.Application.Reports;
using ExpenseTracker.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CurrencyConverter>();
        services.AddScoped<RatesService>();
        services.AddScoped<ICbuRates>(sp => sp.GetRequiredService<RatesService>());
        services.AddScoped<ExchangeRateService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<ReportService>();
        services.AddScoped<SettingService>();
        services.AddScoped<GoldService>();
        return services;
    }
}
