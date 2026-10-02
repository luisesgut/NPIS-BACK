namespace NpiManagement.Domain.Entities;

/// <summary>
/// Solicitud de información levantada por Diseño hacia el Cliente (registrada internamente,
/// sin actor de sistema) o hacia Comercial (con respuesta propia). Ver docs/workflow.md
/// acciones 25-27.
/// </summary>
public sealed class InformationRequest
{
    public Guid Id { get; }
    public InformationRequestType Type { get; private set; }
    public InformationRequestStatus Status { get; private set; }
    public string Text { get; }
    public Guid RequestedByUserId { get; }
    public string RequestedByName { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? ResponseText { get; private set; }

    internal InformationRequest(
        Guid id,
        InformationRequestType type,
        string text,
        Guid requestedByUserId,
        string requestedByName,
        DateTimeOffset requestedAt)
    {
        Id = id;
        Type = type;
        Status = InformationRequestStatus.Pending;
        Text = text;
        RequestedByUserId = requestedByUserId;
        RequestedByName = requestedByName;
        RequestedAt = requestedAt;
    }

    internal void Resolve(DateTimeOffset resolvedAt, Guid resolvedByUserId, string? responseText = null)
    {
        Status = InformationRequestStatus.Resolved;
        ResolvedAt = resolvedAt;
        ResolvedByUserId = resolvedByUserId;
        ResponseText = responseText;
    }
}
