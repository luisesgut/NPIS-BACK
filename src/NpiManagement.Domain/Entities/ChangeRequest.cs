namespace NpiManagement.Domain.Entities;

/// <summary>
/// Solicitud de cambios sobre un NPI. Tres orígenes posibles: Ingeniería→Comercial (rechazo con
/// cambios), Comercial→Diseño (cambios reportados por el cliente, ChangeRequestOrigin.ClientReported),
/// o cualquier otra interna. No es terminal — forma parte del ciclo normal del flujo.
/// </summary>
public sealed class ChangeRequest
{
    public Guid Id { get; }
    public Guid RequestedByUserId { get; }
    public string RequestedByName { get; }
    public DateTimeOffset RequestedAt { get; }
    public AreaType FromArea { get; }
    public AreaType ToArea { get; }
    public ChangeRequestOrigin Origin { get; }
    public string Description { get; }
    public IReadOnlyList<string> FieldKeys { get; }

    internal ChangeRequest(
        Guid id,
        Guid requestedByUserId,
        string requestedByName,
        DateTimeOffset requestedAt,
        AreaType fromArea,
        AreaType toArea,
        ChangeRequestOrigin origin,
        string description,
        IReadOnlyList<string> fieldKeys)
    {
        Id = id;
        RequestedByUserId = requestedByUserId;
        RequestedByName = requestedByName;
        RequestedAt = requestedAt;
        FromArea = fromArea;
        ToArea = toArea;
        Origin = origin;
        Description = description;
        FieldKeys = fieldKeys;
    }
}
