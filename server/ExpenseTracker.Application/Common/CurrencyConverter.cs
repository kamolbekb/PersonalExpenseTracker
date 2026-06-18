namespace ExpenseTracker.Application.Common;

public class CurrencyConverter
{
    public decimal Convert(decimal amount, decimal rate)
        => Math.Round(amount * rate, 2, MidpointRounding.ToEven);
}
