using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Currency;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Features.Budgets;
using ExpenseTracker.Api.Features.Categories;
using ExpenseTracker.Api.Features.Expenses;
using ExpenseTracker.Api.Features.Reports;
using ExpenseTracker.Api.Features.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. Set the ConnectionStrings__Default environment variable.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.Configure<BotOptions>(o => o.Token = builder.Configuration["BotToken"] ?? "");
builder.Services.AddSingleton<TelegramInitDataValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddAuthentication(TelegramAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TelegramAuthHandler>(TelegramAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<CurrencyConverter>();
builder.Services.AddHttpClient<IExchangeRateProvider, FrankfurterRateProvider>(c =>
    c.BaseAddress = new Uri("https://api.frankfurter.app/"));
builder.Services.AddScoped<ExchangeRateService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Text("ok"));

var api = app.MapGroup("/api").RequireAuthorization();

api.MapCategoryEndpoints();
api.MapExpenseEndpoints();
api.MapReportEndpoints();
api.MapBudgetEndpoints();
api.MapSettingEndpoints();

app.Run();

public partial class Program { } // exposes Program to the test host
