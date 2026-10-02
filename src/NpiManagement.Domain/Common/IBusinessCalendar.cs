namespace NpiManagement.Domain.Common;

/// <summary>
/// Calendario laboral usado para calcular el tiempo "hábil" transcurrido en los cronómetros
/// del NPI (ver docs/domain-model.md VO TimerState y docs/architecture-decisions.md ADR-018).
/// </summary>
public interface IBusinessCalendar
{
    bool IsBusinessDay(DateOnly date);

    /// <summary>Milisegundos de tiempo hábil entre dos instantes UTC (0 si end &lt;= start).</summary>
    long BusinessMillisecondsBetween(DateTimeOffset start, DateTimeOffset end);
}
