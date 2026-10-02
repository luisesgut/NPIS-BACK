namespace NpiManagement.Domain.Entities;

/// <summary>
/// Entrada append-only de auditoría de negocio (distinta de TimerEvent, que sólo audita
/// cronómetros). Nunca se actualiza ni se borra. "Details" es texto libre (formato decidido
/// por Application, p. ej. JSON) — Domain no depende de ninguna librería de serialización.
/// </summary>
public sealed class AuditEvent
{
    public Guid Id { get; }
    public Guid ActorUserId { get; }
    public string ActorRole { get; }
    public DateTimeOffset OccurredAt { get; }
    public string Action { get; }
    public string? Details { get; }

    internal AuditEvent(Guid id, Guid actorUserId, string actorRole, DateTimeOffset occurredAt, string action, string? details)
    {
        Id = id;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        OccurredAt = occurredAt;
        Action = action;
        Details = details;
    }
}
