using System.ComponentModel.DataAnnotations;
using NpiManagement.Domain;

namespace NpiManagement.Api.Contracts.Npis;

// Ver docs/api-contract.md §4. Ninguna acción admite "status" genérico — cada una tiene su
// propio contrato, alineado 1:1 con la firma del método de dominio correspondiente en Npi.cs.

public sealed record AssignUserRequest([Required] Guid UserId);

/// <summary>Equipo por NPI (2026-11-10): persona y área en la que participa.</summary>
public sealed record NpiParticipantRequest([Required] Guid UserId, [Required] NpiManagement.Domain.AreaType Area);

/// <summary>Asignar Diseño: el admin clasifica el arte en el mismo paso (obligatorio si el NPI aún no tiene clasificación).</summary>
public sealed record AssignDesignRequest([Required] Guid UserId, ArtClassification? ArtClassification, bool? HasLabValues);

public sealed record NoteRequest(string? Note);

/// <summary>Comercial modifica el NPI por su cuenta: motivo (lo ven Ingeniería y Diseño) y si el cambio afecta el PrintCard.</summary>
public sealed record StartCommercialEditRequest([Required] string Reason, bool RedoPrintCard = false);

/// <summary>Reabrir un NPI cerrado por error: el motivo es obligatorio (queda en el historial).</summary>
public sealed record ReopenNpiRequest([Required] string Reason);

/// <summary>AdminTotal corrige qué área pidió una solicitud de cambios pendiente (Ingenieria o Diseno).</summary>
public sealed record CorrectChangeRequestAreaRequest(NpiManagement.Domain.AreaType Area, [Required] string Reason);

public sealed record EngineeringFeasibilityRequest(string? Note, IReadOnlyList<string>? ConfirmedFieldKeys);

/// <summary>Factibilidad de Diseño: clasificación del arte obligatoria; HasLabValues sólo si el producto lleva Base Pantone.</summary>
public sealed record DesignFeasibilityRequest(string? Note, ArtClassification? ArtClassification, bool? HasLabValues);

public sealed record DesignClassificationRequest(ArtClassification? ArtClassification, bool? HasLabValues);

/// <summary>FieldNotes: comentario opcional por campo marcado (clave del campo → qué corregir).</summary>
/// <param name="RedoPrintCard">Sólo Ingeniería después de su factibilidad: el cambio afecta el PrintCard y Diseño debe rehacerlo.</param>
public sealed record RequestChangesRequest([Required] string Description, IReadOnlyList<string>? FieldKeys, IReadOnlyDictionary<string, string>? FieldNotes = null, bool RedoPrintCard = false, IReadOnlyList<Guid>? ImageAttachmentIds = null);

public sealed record CreatePrintCardVersionRequest([Required] string Code, Guid? AttachmentId, string? Note, IReadOnlyList<string>? CommercialChecklist = null);

public sealed record ApprovePrintCardRequest([Required] ApproverRole Role, string? Note, Guid? EvidenceAttachmentId);

public sealed record DescriptionRequest([Required] string Description);

/// <summary>Cambios que pidió el cliente: un renglón por cambio (Diseño los palomea) y un comentario general opcional.</summary>
/// <summary>AttachmentIds: capturas, fotos o PDF (adjuntos del NPI) que mandó el cliente con sus cambios.</summary>
public sealed record ClientChangesRequest(string? Description, IReadOnlyList<string>? Items, IReadOnlyList<Guid>? AttachmentIds = null);

/// <param name="Comment">Comentario del punto al palomearlo (opcional; 2026-11-09).</param>
public sealed record ClientChangeItemRequest(bool Done, string? Comment = null);

public sealed record RejectPrintCardRequest([Required] string Reason, Guid? EvidenceAttachmentId);

public sealed record RejectByClientRequest([Required] string Comment, Guid? EvidenceAttachmentId);

/// <summary>SapTable: tabla copiada de Excel/SAP (texto separado por tabuladores), opcional.</summary>
public sealed record RegisterSapPtRequest([Required] string CodigoPT, string? PtName, string? SapTable = null);

public sealed record RegisterSapOrderRequest([Required] string PedidoSapNumber);

public sealed record ResolveSuppliesAgreementRequest([Required] string Text, [Required] Guid EvidenceAttachmentId);

public sealed record RequestInformationRequest([Required] InformationRequestType Type, [Required] string Text, IReadOnlyList<Guid>? ImageAttachmentIds = null);

public sealed record RespondCommercialInformationRequest([Required] string ResponseText);

public sealed record RespondEngineeringInformationRequest([Required] string ResponseText);

public sealed record CancelRequest([Required] string Reason);

/// <summary>Rechazos de Diseño: el motivo es obligatorio (es lo que lee Comercial).</summary>
public sealed record RequiredNoteRequest([Required] string Note);

/// <summary>Visto bueno a una propuesta de cierre: el comentario del admin es opcional.</summary>
public sealed record ConfirmClosureProposalRequest(string? Comment);

/// <summary>No autorizar una propuesta de cierre: el comentario es obligatorio (lo lee quien la propuso).</summary>
public sealed record DeclineClosureProposalRequest([Required] string Comment);

public sealed record RejectArtworkRequest([Required] string Reason, Guid? EvidenceAttachmentId);

// ===== Cotización en SISPRO =====

public sealed record LinkSisproQuoteRequest([Required] string NumeroCotizacion);

public sealed record AcceptSisproQuoteRequest(string? Note, Guid? EvidenceAttachmentId);

public sealed record RejectByPriceRequest([Required] string Comment, Guid? EvidenceAttachmentId);
