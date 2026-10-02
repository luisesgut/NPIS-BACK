using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Tests;

public sealed class TestClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; } = start;

    public static TestClock Default() => new(new DateTimeOffset(2026, 1, 5, 16, 0, 0, TimeSpan.Zero));
}
