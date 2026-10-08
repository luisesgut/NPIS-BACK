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

    /// <summary>Si en ese instante corre el tiempo hábil (un cronómetro en marcha avanza).</summary>
    bool IsBusinessTime(DateTimeOffset instant);

    /// <summary>
    /// Próximo instante en que cambia <see cref="IsBusinessTime"/>: el cierre de la jornada si es hora
    /// hábil, o el inicio de la siguiente jornada hábil si no. Lo usa el front para saber hasta cuándo
    /// puede seguir sumando segundos sin preguntar al servidor.
    /// </summary>
    DateTimeOffset NextBoundary(DateTimeOffset instant);
}
