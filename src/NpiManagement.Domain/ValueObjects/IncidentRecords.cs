namespace NpiManagement.Domain.ValueObjects;

/// <summary>
/// Tres grupos de columnas embebidas que ocurren, cada uno, cuando mucho una vez por NPI
/// (no son historial repetible) — ver docs/domain-model.md §2.2.
/// </summary>
public sealed record SuppliesTemporaryRejectionInfo(DateTimeOffset At, Guid ByUserId, string? Note);

public sealed record SuppliesAgreementInfo(string Text, DateTimeOffset ResolvedAt, Guid ResolvedByUserId, Guid EvidenceAttachmentId);

/// <summary>Rechazo definitivo del cliente (decisión 7) — comentario obligatorio, evidencia opcional.</summary>
public sealed record ClientRejectionInfo(DateTimeOffset At, Guid ByUserId, string Comment, Guid? EvidenceAttachmentId);
