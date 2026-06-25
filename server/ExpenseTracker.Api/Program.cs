using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Endpoints;
using ExpenseTracker.Application;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<BotOptions>(o => o.Token = builder.Configuration["BotToken"] ?? "");
builder.Services.AddAuthentication(TelegramAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TelegramAuthHandler>(TelegramAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Text("ok"));

app.MapPost("/internal/refresh", async (
    HttpRequest request, IServiceProvider services, IClock clock,
    IConfiguration config, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var configured = config["Rates:RefreshToken"];
    var provided = request.Headers["X-Refresh-Token"].ToString();
    if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(provided) ||
        !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(configured)))
    {
        return Results.Unauthorized();
    }

    var today = clock.TodayInTashkent();
    var logger = loggerFactory.CreateLogger("RefreshEndpoint");
    await DailyRateFetchService.RunOnceAsync(services, today, logger, ct);
    return Results.Ok(new { refreshed = today.ToString("yyyy-MM-dd") });
});

var api = app.MapGroup("/api").RequireAuthorization();
api.MapExpenseEndpoints();
api.MapCategoryEndpoints();
api.MapIncomeCategoryEndpoints();
api.MapIncomeEndpoints();
api.MapBudgetEndpoints();
api.MapReportEndpoints();
api.MapSettingEndpoints();
api.MapRatesEndpoints();
api.MapGoldEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
