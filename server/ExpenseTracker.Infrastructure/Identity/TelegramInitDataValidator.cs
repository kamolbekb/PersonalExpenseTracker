using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExpenseTracker.Infrastructure.Identity;

public class TelegramInitDataValidator
{
    static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    public bool TryValidate(string initData, string botToken, DateTimeOffset now, out TelegramUser user)
    {
        user = default!;
        if (string.IsNullOrEmpty(initData)) return false;

        var pairs = initData.Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]));

        if (!pairs.TryGetValue("hash", out var providedHash)) return false;
        if (!pairs.TryGetValue("auth_date", out var authRaw)
            || !long.TryParse(authRaw, out var authUnix)) return false;
        if (!pairs.TryGetValue("user", out var userJson)) return false;

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authUnix);
        if (now - authDate > MaxAge) return false;

        var dataCheckString = string.Join("\n", pairs
            .Where(kv => kv.Key != "hash")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"),
                                         Encoding.UTF8.GetBytes(botToken));
        var computed = Convert.ToHexString(
            new HMACSHA256(secret).ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(providedHash.ToLowerInvariant()))) return false;

        try
        {
            using var doc = JsonDocument.Parse(userJson);
            var root = doc.RootElement;
            user = new TelegramUser(
                root.GetProperty("id").GetInt64(),
                root.TryGetProperty("first_name", out var f) ? f.GetString() : null,
                root.TryGetProperty("username", out var u) ? u.GetString() : null);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
