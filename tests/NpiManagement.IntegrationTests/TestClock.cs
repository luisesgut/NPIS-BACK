using NpiManagement.Domain.Common;

namespace NpiManagement.IntegrationTests;

/// <summary>Reloj determinista para pruebas de integración (no depende del reloj del sistema).</summary>
public sealed class TestClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; } = start;
}
