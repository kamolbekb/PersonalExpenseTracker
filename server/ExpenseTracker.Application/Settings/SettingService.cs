using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Settings;

public class SettingService(IApplicationDbContext db)
{
    public async Task<OperationResult<SettingDto>> GetAsync(int userId)
    {
        var s = await db.Settings.FirstAsync(x => x.UserId == userId);
        return OperationResult<SettingDto>.Ok(new SettingDto(s.BaseCurrency, s.IncomeTrackingEnabled));
    }

    public async Task<OperationResult<SettingDto>> UpdateAsync(int userId, SettingDto input)
    {
        if (string.IsNullOrWhiteSpace(input.BaseCurrency)) return OperationResult<SettingDto>.Bad("Base currency required.");
        var s = await db.Settings.FirstAsync(x => x.UserId == userId);
        s.BaseCurrency = input.BaseCurrency.ToUpperInvariant();
        s.IncomeTrackingEnabled = input.IncomeTrackingEnabled;
        await db.SaveChangesAsync();
        return OperationResult<SettingDto>.Ok(new SettingDto(s.BaseCurrency, s.IncomeTrackingEnabled));
    }
}
