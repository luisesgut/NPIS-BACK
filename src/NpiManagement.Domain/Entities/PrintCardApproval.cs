namespace NpiManagement.Domain.Entities;

/// <summary>
/// Una de las tres aprobaciones (Diseño/Ingeniería/Cliente) de una versión concreta de PrintCard.
/// La aprobación de rol Client exige evidencia obligatoria y siempre la registra un usuario
/// Comercial en nombre del cliente (decisión 8 — el cliente no es un actor autenticado).
/// </summary>
public sealed class PrintCardApproval
{
    public Guid Id { get; }
    public Guid PrintCardVersionId { get; }
    public ApproverRole Role { get; }
    public Guid ByUserId { get; }
    public string ByName { get; }
    public DateTimeOffset At { get; }
    public string? Note { get; }
    public Guid? EvidenceAttachmentId { get; }

    internal PrintCardApproval(
        Guid id,
        Guid printCardVersionId,
        ApproverRole role,
        Guid byUserId,
        string byName,
        DateTimeOffset at,
        string? note,
        Guid? evidenceAttachmentId)
    {
        if (role == ApproverRole.Client && evidenceAttachmentId is null)
            throw new Exceptions.NpiValidationException(
                "client-approval-evidence-required",
                "La aprobación en nombre del Cliente requiere evidencia obligatoria (PrintCard firmado, correo, etc.).");

        Id = id;
        PrintCardVersionId = printCardVersionId;
        Role = role;
        ByUserId = byUserId;
        ByName = byName;
        At = at;
        Note = note;
        EvidenceAttachmentId = evidenceAttachmentId;
    }
}
