namespace NpiManagement.Domain.Entities;

/// <summary>
/// Un campo de la captura que cambió después de enviar el NPI: valor anterior y nuevo, quién y
/// cuándo, y qué solicitud de cambios lo pidió. Vive fuera del agregado (como el chat): sólo es
/// historia, no interviene en ninguna regla del flujo, y no debe cargarse en cada acción.
/// </summary>
public sealed class CaptureChange
{
    public Guid Id { get; }
    public Guid NpiId { get; }
    /// <summary>Clave del campo en camelCase (como en el frontend); "priority" para la prioridad.</summary>
    public string Field { get; }
    public string Before { get; }
    public string After { get; }
    public Guid ChangedByUserId { get; }
    public string ChangedByName { get; }
    public DateTimeOffset ChangedAt { get; }

    /// <summary>Solicitud de cambios que estaba abierta al hacer el cambio (null = por iniciativa de Comercial).</summary>
    public Guid? ChangeRequestId { get; }

    /// <summary>true si esa solicitud marcó este campo; false si se cambió mientras se atendía otra cosa de la solicitud.</summary>
    public bool WasRequested { get; }

    internal CaptureChange(Guid id, Guid npiId, string field, string before, string after, Guid changedByUserId, string changedByName, DateTimeOffset changedAt, Guid? changeRequestId, bool wasRequested)
    {
        Id = id;
        NpiId = npiId;
        Field = field;
        Before = before;
        After = after;
        ChangedByUserId = changedByUserId;
        ChangedByName = changedByName;
        ChangedAt = changedAt;
        ChangeRequestId = changeRequestId;
        WasRequested = wasRequested;
    }
}
