using FluentAssertions;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Domain.Tests.ValueObjects;

public class NpiNumberTests
{
    [Theory]
    [InlineData(4, "NPI-0004")]
    [InlineData(1, "NPI-0001")]
    [InlineData(12345, "NPI-12345")]
    public void FromSequenceValue_formats_with_at_least_4_digits(long sequenceValue, string expected)
    {
        NpiNumber.FromSequenceValue(sequenceValue).Value.Should().Be(expected);
    }

    [Fact]
    public void FromSequenceValue_rejects_non_positive_values()
    {
        var act = () => NpiNumber.FromSequenceValue(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("NPI-0001")]
    [InlineData("NPI-1006")] // rango semilla, ver docs/architecture-decisions.md ADR-005
    public void FromExisting_accepts_valid_formats(string value)
    {
        NpiNumber.FromExisting(value).Value.Should().Be(value);
    }

    [Theory]
    [InlineData("NPI-1")]
    [InlineData("NPI-")]
    [InlineData("0004")]
    [InlineData("npi-0004")]
    [InlineData("")]
    public void FromExisting_rejects_invalid_formats(string value)
    {
        var act = () => NpiNumber.FromExisting(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_is_by_value()
    {
        NpiNumber.FromExisting("NPI-0001").Should().Be(NpiNumber.FromExisting("NPI-0001"));
        NpiNumber.FromExisting("NPI-0001").Should().NotBe(NpiNumber.FromExisting("NPI-0002"));
    }
}
