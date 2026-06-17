using ExpenseTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. Set the ConnectionStrings__Default environment variable.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

var app = builder.Build();

app.MapGet("/healthz", () => Results.Text("ok"));

app.Run();

public partial class Program { } // exposes Program to the test host
