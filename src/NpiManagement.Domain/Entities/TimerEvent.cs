namespace NpiManagement.Domain.Entities;

/// <summary>Entrada append-only de la bitácora de cronómetros. Nunca se actualiza ni se borra.</summary>
public sealed class TimerEvent
{
    public Guid Id { get; }
    public TimerStage Stage { get; }
    public TimerAction Action { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid? ActorUserId { get; }
    public long? DurationMsAtEvent { get; }
    public TimerStage? FromStage { get; }
    public TimerStage? ToStage { get; }
    public string? Note { get; }

    internal TimerEvent(
        Guid id,
        TimerStage stage,
        TimerAction action,
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        long? durationMsAtEvent = null,
        TimerStage? fromStage = null,
        TimerStage? toStage = null,
        string? note = null)
    {
        Id = id;
        Stage = stage;
        Action = action;
        OccurredAt = occurredAt;
        ActorUserId = actorUserId;
        DurationMsAtEvent = durationMsAtEvent;
        FromStage = fromStage;
        ToStage = toStage;
        Note = note;
    }
}
