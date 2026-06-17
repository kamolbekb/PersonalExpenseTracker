using ExpenseTracker.Api.Auth;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class TelegramInitDataValidatorTests
{
    const string BotToken = "123456:TESTBOTTOKEN";
    readonly TelegramInitDataValidator _sut = new();
    readonly DateTimeOffset _now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_initData_returns_true_and_parses_user()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddMinutes(-1), "Alice", "alice");

        var ok = _sut.TryValidate(initData, BotToken, _now, out var user);

        ok.Should().BeTrue();
        user.Id.Should().Be(42);
        user.FirstName.Should().Be("Alice");
        user.Username.Should().Be("alice");
    }

    [Fact]
    public void Tampered_hash_returns_false()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddMinutes(-1));
        var tampered = initData[..^4] + "0000";

        _sut.TryValidate(tampered, BotToken, _now, out _).Should().BeFalse();
    }

    [Fact]
    public void Wrong_bot_token_returns_false()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddMinutes(-1));

        _sut.TryValidate(initData, "999:WRONG", _now, out _).Should().BeFalse();
    }

    [Fact]
    public void Expired_auth_date_returns_false()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddHours(-25));

        _sut.TryValidate(initData, BotToken, _now, out _).Should().BeFalse();
    }

    [Fact]
    public void Missing_hash_returns_false()
    {
        _sut.TryValidate("user=%7B%22id%22%3A1%7D&auth_date=1", BotToken, _now, out _)
            .Should().BeFalse();
    }
}
