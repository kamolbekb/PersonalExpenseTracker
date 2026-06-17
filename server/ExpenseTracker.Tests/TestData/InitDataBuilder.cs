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
        var payload = new Dictionary<string, object> { ["id"] = userId };
        if (firstName is not null) payload["first_name"] = firstName;
        if (username is not null) payload["username"] = username;
        return BuildWithRawUser(JsonSerializer.Serialize(payload), botToken, authDate);
    }

    public static string BuildWithRawUser(string userJson, string botToken, DateTimeOffset authDate)
    {
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
