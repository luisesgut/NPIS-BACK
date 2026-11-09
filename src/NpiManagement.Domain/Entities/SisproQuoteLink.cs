using NpiManagement.Domain.Common;

namespace NpiManagement.Domain.Entities;

/// <summary>
/// Cotización de SISPRO ligada al NPI. La cotización vive en SISPRO (cotizador oficial, decisión de
/// TI del 2026-10-27); aquí se guarda el número, la foto de lo que SISPRO decía al ligarla y la
/// respuesta del cliente al precio. SnapshotJson es opaco para el dominio: lo arma Application con
/// lo que leyó de SISPRO (cantidades, precios, capas) para mostrarlo sin volver a consultar.
/// </summary>
public sealed class SisproQuoteLink
{
    public Guid Id { get; }
    public int VersionNumber { get; }
    public string NumeroCotizacion { get; }
    /// <summary>Estado en SISPRO al ligarla ("Cotizada", "Aceptada"...).</summary>
    public string EstadoSispro { get; }
    public string SnapshotJson { get; }
    /// <summary>Diferencias entre la estructura cotizada y la del NPI, una por renglón (vacía si coinciden).</summary>
    public IReadOnlyList<string> StructureWarnings { get; }
    public Guid LinkedByUserId { get; }
    public string LinkedByName { get; }
    public DateTimeOffset LinkedAt { get; }

    public SisproQuoteStatus Status { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public Guid? RespondedByUserId { get; private set; }
    public string? RespondedByName { get; private set; }
    public string? ResponseNote { get; private set; }
    public Guid? EvidenceAttachmentId { get; private set; }
    public DateTimeOffset? InvalidatedAt { get; private set; }
    public string? InvalidationReason { get; private set; }

    internal SisproQuoteLink(Guid id, int versionNumber, string numeroCotizacion, string estadoSispro, string snapshotJson,
        IReadOnlyList<string> structureWarnings, Guid linkedByUserId, string linkedByName, DateTimeOffset linkedAt)
    {
        Id = id;
        VersionNumber = versionNumber;
        NumeroCotizacion = numeroCotizacion;
        EstadoSispro = estadoSispro;
        SnapshotJson = snapshotJson;
        StructureWarnings = structureWarnings;
        LinkedByUserId = linkedByUserId;
        LinkedByName = linkedByName;
        LinkedAt = linkedAt;
        Status = SisproQuoteStatus.AwaitingClient;
    }

    internal void Respond(SisproQuoteStatus status, Actor actor, DateTimeOffset at, string? note, Guid? evidenceAttachmentId)
    {
        Status = status;
        RespondedAt = at;
        RespondedByUserId = actor.UserId;
        RespondedByName = actor.Name;
        ResponseNote = note;
        EvidenceAttachmentId = evidenceAttachmentId;
    }

    internal void Supersede() => Status = SisproQuoteStatus.Superseded;

    internal void Invalidate(DateTimeOffset at, string reason)
    {
        Status = SisproQuoteStatus.Invalidated;
        InvalidatedAt = at;
        InvalidationReason = reason;
    }
}
