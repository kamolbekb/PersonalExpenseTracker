using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Gold;
using ExpenseTracker.Application.Rates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Infrastructure.Scheduling;

public class DailyRateFetchService(
    IServiceScopeFactory scopeFactory, IClock clock, ILogger<DailyRateFetchService> logger,
    IConfiguration config) : BackgroundService
{
    static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FetchTodayAsync(stoppingToken);                       // startup catch-up
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            try { await Task.Delay(delay, stoppingToken); } catch (TaskCanceledException) { break; }
            await FetchTodayAsync(stoppingToken);
        }
    }

    TimeSpan TimeUntilNextRun()
    {
        var hour = config.GetValue("Rates:DailyFetchHourTashkent", 9);
        var nowTz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz);
        var next = new DateTimeOffset(nowTz.Year, nowTz.Month, nowTz.Day, hour, 0, 0, nowTz.Offset);
        if (next <= nowTz) next = next.AddDays(1);
        return next - nowTz;
    }

    async Task FetchTodayAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await RunOnceAsync(scope.ServiceProvider, clock.TodayInTashkent(), logger, ct);
    }

    public static async Task RunOnceAsync(IServiceProvider sp, DateOnly date, ILogger logger, CancellationToken ct)
    {
        var rates = sp.GetRequiredService<RatesService>();
        var gold = sp.GetRequiredService<GoldService>();
        try { await rates.EnsureAllSourcesForDateAsync(date, ct); }
        catch (Exception ex) { logger.LogError(ex, "Rate fetch failed for {Date}", date); }
        try { await gold.EnsureForDateAsync(date, ct); }
        catch (Exception ex) { logger.LogError(ex, "Gold fetch failed for {Date}", date); }
    }
}
