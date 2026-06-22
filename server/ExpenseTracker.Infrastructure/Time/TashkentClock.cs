using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Infrastructure.Time;

public class TashkentClock : IClock
{
    static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateOnly TodayInTashkent() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz).DateTime);
}
