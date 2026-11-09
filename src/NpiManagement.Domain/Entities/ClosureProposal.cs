using NpiManagement.Domain.Common;

namespace NpiManagement.Domain.Entities;

/// <summary>
/// Propuesta de cerrar el NPI que hace un usuario normal (no admin). Los 5 cierres que puede
/// ejecutar un usuario de área (no factibilidad de Ingeniería o de Diseño, rechazo definitivo del
/// cliente, rechazo total por insumos y rechazo por precio) no cierran el NPI por sí solos: quedan
/// aquí hasta que el admin de esa área (o AdminTotal) da el visto bueno o no lo autoriza. Todas
/// quedan como historial, también las no autorizadas y las retiradas.
/// </summary>
public sealed class ClosureProposal
{
    public Guid Id { get; }
    /// <summary>El cierre que se propone (el mismo motivo con el que quedará cerrado si se confirma).</summary>
    public TerminalReason Reason { get; }
    /// <summary>Área a la que pertenece el cierre: sólo su admin (o AdminTotal) lo autoriza.</summary>
    public AreaType Area { get; }
    public Guid ProposedByUserId { get; }
    public string ProposedByName { get; }
    public DateTimeOffset ProposedAt { get; }
    public string Note { get; }
    public Guid? EvidenceAttachmentId { get; }

    public ClosureProposalStatus Status { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecidedByName { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    // Los nombres de los parámetros coinciden con las propiedades: EF Core materializa con este constructor.
    internal ClosureProposal(Guid id, TerminalReason reason, AreaType area, Guid proposedByUserId, string proposedByName, DateTimeOffset proposedAt, string note, Guid? evidenceAttachmentId)
    {
        Id = id;
        Reason = reason;
        Area = area;
        ProposedByUserId = proposedByUserId;
        ProposedByName = proposedByName;
        ProposedAt = proposedAt;
        Note = note;
        EvidenceAttachmentId = evidenceAttachmentId;
        Status = ClosureProposalStatus.Pending;
    }

    internal void Decide(ClosureProposalStatus status, Actor by, DateTimeOffset at, string? note)
    {
        Status = status;
        DecidedByUserId = by.UserId;
        DecidedByName = by.Name;
        DecidedAt = at;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>A qué área pertenece cada cierre que puede proponerse.</summary>
    public static AreaType AreaOf(TerminalReason reason) => reason switch
    {
        TerminalReason.NoFeasibility => AreaType.Ingenieria,
        TerminalReason.DesignNoFeasibility => AreaType.Diseno,
        _ => AreaType.Comercial,
    };
}
