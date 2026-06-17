using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Api.Auth;

public class TelegramAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "tma";
    readonly TelegramInitDataValidator _validator;
    readonly BotOptions _bot;

    public TelegramAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder,
        TelegramInitDataValidator validator, IOptions<BotOptions> bot)
        : base(options, logger, encoder)
    {
        _validator = validator;
        _bot = bot.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith($"{SchemeName} ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var initData = header[(SchemeName.Length + 1)..];
        if (!_validator.TryValidate(initData, _bot.Token, DateTimeOffset.UtcNow, out var tgUser))
            return Task.FromResult(AuthenticateResult.Fail("Invalid initData"));

        var claims = new List<Claim> { new("tg_id", tgUser.Id.ToString()) };
        if (tgUser.FirstName is not null) claims.Add(new(ClaimTypes.GivenName, tgUser.FirstName));
        if (tgUser.Username is not null) claims.Add(new("tg_username", tgUser.Username));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
