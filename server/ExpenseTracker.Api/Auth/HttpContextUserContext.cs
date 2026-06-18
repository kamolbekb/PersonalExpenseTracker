using System.Security.Claims;
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Api.Auth;

public class HttpContextUserContext(IHttpContextAccessor http) : IUserContext
{
    public long? TelegramUserId
    {
        get
        {
            var val = http.HttpContext?.User.FindFirstValue("tg_id");
            return val is null ? null : long.Parse(val);
        }
    }

    public string? FirstName => http.HttpContext?.User.FindFirstValue(ClaimTypes.GivenName);
    public string? Username => http.HttpContext?.User.FindFirstValue("tg_username");
}
