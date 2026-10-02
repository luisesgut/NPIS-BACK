using FluentAssertions;
using NpiManagement.Infrastructure.Auth;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>No requiere base de datos ni Docker — corre siempre.</summary>
public class TemporaryPasswordGeneratorTests
{
    private static IEnumerable<string> Samples(int count) => Enumerable.Range(0, count).Select(_ => TemporaryPasswordGenerator.Create());

    [Fact]
    public void Always_meets_the_identity_password_policy()
    {
        foreach (var password in Samples(500))
        {
            password.Should().HaveLength(TemporaryPasswordGenerator.Length);
            password.Should().MatchRegex("[A-Z]").And.MatchRegex("[a-z]").And.MatchRegex("[0-9]").And.MatchRegex("[^A-Za-z0-9]");
        }
    }

    [Fact]
    public void Never_uses_characters_that_get_confused_when_read_aloud()
    {
        foreach (var password in Samples(500))
            password.Should().NotContainAny("0", "O", "1", "l", "I");
    }

    [Fact]
    public void Does_not_repeat_passwords()
    {
        Samples(200).Distinct().Should().HaveCount(200);
    }
}
