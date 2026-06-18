namespace ExpenseTracker.Application.Common.Interfaces;

public interface IClock
{
    DateTimeOffset Now { get; }
    DateOnly TodayInTashkent();
}
