using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
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
builder.Services.AddAuthentication(TelegramAuthHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, TelegramAuthHandler>(TelegramAuthHandler.Scheme, _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Text("ok"));

var api = app.MapGroup("/api").RequireAuthorization();
// Placeholder so the auth test has a route; replaced in Task 5+.
api.MapGet("/categories", async (ICurrentUser cu, AppDbContext db) =>
{
    var user = await cu.GetOrCreateAsync();
    return Results.Ok(await db.Categories.Where(c => c.UserId == user.Id).ToListAsync());
});

app.Run();

public partial class Program { } // exposes Program to the test host
