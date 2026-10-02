using NpiManagement.Domain.Common;

namespace NpiManagement.Domain.Tests;

/// <summary>Reloj determinista para pruebas: arranca en un instante fijo y avanza sólo cuando se le pide.</summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; }

    public TestClock(DateTimeOffset start) => UtcNow = start;

    /// <summary>Lunes 10:00 America/Mexico_City (16:00 UTC) — dentro de horario hábil, por defecto para la mayoría de las pruebas.</summary>
    public static TestClock MondayMorning() => new(new DateTimeOffset(2026, 1, 5, 16, 0, 0, TimeSpan.Zero));

    public void Advance(TimeSpan span) => UtcNow += span;

    public void Set(DateTimeOffset value) => UtcNow = value;
}
