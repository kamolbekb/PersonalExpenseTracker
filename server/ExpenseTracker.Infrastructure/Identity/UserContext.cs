using System.Security.Claims;
using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Infrastructure.Identity;

public class UserContext(IHttpContextAccessor accessor) : IUserContext
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public long? TelegramUserId =>
        long.TryParse(Principal?.FindFirstValue("tg_id"), out var id) ? id : null;
    public string? FirstName => Principal?.FindFirstValue(ClaimTypes.GivenName);
    public string? Username => Principal?.FindFirstValue("tg_username");
}
