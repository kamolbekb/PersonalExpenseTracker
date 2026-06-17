using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExpenseTracker.Tests.TestData;

public static class InitDataBuilder
{
    public static string Build(long userId, string botToken, DateTimeOffset authDate,
        string? firstName = "Test", string? username = "tester")
    {
        var userJson = JsonSerializer.Serialize(new
        {
            id = userId, first_name = firstName, username
        });
        var fields = new SortedDictionary<string, string>
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(),
            ["user"] = userJson,
        };
        var dataCheckString = string.Join("\n", fields.Select(kv => $"{kv.Key}={kv.Value}"));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"),
                                         Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexString(
            new HMACSHA256(secret).ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();
        var parts = fields.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}").ToList();
        parts.Add($"hash={hash}");
        return string.Join("&", parts);
    }
}
