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
    public string Text { get; private set; }
    public Guid RequestedByUserId { get; }
    public string RequestedByName { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? ResponseText { get; private set; }

    /// <summary>Capturas de pantalla o fotos adjuntas al pedirla (ids de Attachments del NPI, separados por coma).</summary>
    public string? ImageAttachmentIdsCsv { get; private set; }
    public IReadOnlyList<Guid> ImageAttachmentIds => RequestImages.Parse(ImageAttachmentIdsCsv);
    internal void SetImages(IReadOnlyList<Guid> ids) => ImageAttachmentIdsCsv = RequestImages.Join(ids);

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

    /// <summary>Quien la pidió la corrige o le agrega algo antes de que se la contesten.</summary>
    internal void Edit(string text) => Text = text;

    /// <summary>Quien la pidió la retira: ya no hace falta.</summary>
    internal void Withdraw(DateTimeOffset at, Guid byUserId)
    {
        Status = InformationRequestStatus.Withdrawn;
        ResolvedAt = at;
        ResolvedByUserId = byUserId;
    }

    internal void Resolve(DateTimeOffset resolvedAt, Guid resolvedByUserId, string? responseText = null)
    {
        Status = InformationRequestStatus.Resolved;
        ResolvedAt = resolvedAt;
        ResolvedByUserId = resolvedByUserId;
        ResponseText = responseText;
    }
}
