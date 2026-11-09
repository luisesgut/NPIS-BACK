using NpiManagement.Domain.Common;

namespace NpiManagement.Domain.ValueObjects;

/// <summary>
/// Cronómetro con soporte de pausa/reanudación, medido en tiempo hábil.
/// Inmutable: cada operación devuelve una nueva instancia. Nunca se persiste un contador
/// incrementando cada segundo — se guarda el acumulado + el instante de reanudación, y el
/// tiempo transcurrido se calcula al leer (ver docs/domain-model.md, VO TimerState).
/// Puerto directo de src/lib/timers.ts del frontend.
/// </summary>
public sealed class TimerState : IEquatable<TimerState>
{
    public DateTimeOffset? StartAt { get; }
    public DateTimeOffset? EndAt { get; }
    public long AccumulatedBusinessMs { get; }
    public DateTimeOffset? RunningSinceAt { get; }

    public static readonly TimerState NotStarted = new(null, null, 0, null);

    private TimerState(DateTimeOffset? startAt, DateTimeOffset? endAt, long accumulatedBusinessMs, DateTimeOffset? runningSinceAt)
    {
        if (accumulatedBusinessMs < 0)
            throw new ArgumentOutOfRangeException(nameof(accumulatedBusinessMs), "El acumulado no puede ser negativo.");

        StartAt = startAt;
        EndAt = endAt;
        AccumulatedBusinessMs = accumulatedBusinessMs;
        RunningSinceAt = runningSinceAt;
    }

    public bool HasStarted => StartAt is not null;
    public bool IsClosed => EndAt is not null;
    public bool IsRunning => RunningSinceAt is not null && EndAt is null;

    /// <summary>Arranca el cronómetro si nunca inició; lo reanuda si estaba en pausa; no hace nada si ya corre o está cerrado.</summary>
    public TimerState StartOrResume(IClock clock)
    {
        var now = clock.UtcNow;
        if (!HasStarted)
            return new TimerState(now, null, 0, now);

        if (IsClosed || IsRunning)
            return this; // cerrado: nunca se reabre; corriendo: no-op

        return new TimerState(StartAt, null, AccumulatedBusinessMs, now);
    }

    /// <summary>Pausa el cronómetro, acumulando el tiempo hábil transcurrido desde la última reanudación.</summary>
    public TimerState Pause(IClock clock, IBusinessCalendar calendar)
    {
        if (!HasStarted || IsClosed || RunningSinceAt is null)
            return this;

        var now = clock.UtcNow;
        var delta = calendar.BusinessMillisecondsBetween(RunningSinceAt.Value, now);
        return new TimerState(StartAt, null, AccumulatedBusinessMs + Math.Max(0, delta), null);
    }

    /// <summary>Pausa (si corría) y cierra definitivamente el cronómetro.</summary>
    public TimerState Close(IClock clock, IBusinessCalendar calendar)
    {
        if (!HasStarted)
            return this;
        if (IsClosed)
            return this;

        var paused = Pause(clock, calendar);
        return new TimerState(paused.StartAt, clock.UtcNow, paused.AccumulatedBusinessMs, null);
    }

    /// <summary>
    /// Reabre un cronómetro cerrado, en pausa y con su acumulado intacto (sólo al reabrir un NPI que se
    /// cerró por error; el tiempo que estuvo cerrado no se cuenta).
    /// </summary>
    public TimerState Reopen() => IsClosed ? new TimerState(StartAt, null, AccumulatedBusinessMs, null) : this;

    /// <summary>Tiempo hábil transcurrido hasta ahora (acumulado + tramo activo si corre).</summary>
    public long ElapsedMs(IClock clock, IBusinessCalendar calendar)
    {
        if (!HasStarted)
            return 0;

        var total = AccumulatedBusinessMs;
        if (IsRunning)
            total += calendar.BusinessMillisecondsBetween(RunningSinceAt!.Value, clock.UtcNow);

        return total;
    }

    public bool Equals(TimerState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return StartAt == other.StartAt && EndAt == other.EndAt &&
               AccumulatedBusinessMs == other.AccumulatedBusinessMs && RunningSinceAt == other.RunningSinceAt;
    }

    public override bool Equals(object? obj) => Equals(obj as TimerState);

    public override int GetHashCode() => HashCode.Combine(StartAt, EndAt, AccumulatedBusinessMs, RunningSinceAt);
}
