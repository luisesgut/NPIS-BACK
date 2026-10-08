using System.ComponentModel.DataAnnotations;
using NpiManagement.Domain;

namespace NpiManagement.Api.Contracts.Npis;

// Ver docs/api-contract.md §4. Ninguna acción admite "status" genérico — cada una tiene su
// propio contrato, alineado 1:1 con la firma del método de dominio correspondiente en Npi.cs.

public sealed record AssignUserRequest([Required] Guid UserId);

public sealed record NoteRequest(string? Note);

public sealed record EngineeringFeasibilityRequest(string? Note, IReadOnlyList<string>? ConfirmedFieldKeys);

public sealed record RequestChangesRequest([Required] string Description, IReadOnlyList<string>? FieldKeys);

public sealed record CreatePrintCardVersionRequest([Required] string Code, Guid? AttachmentId, string? Note);

public sealed record ApprovePrintCardRequest([Required] ApproverRole Role, string? Note, Guid? EvidenceAttachmentId);

public sealed record DescriptionRequest([Required] string Description);

public sealed record RejectByClientRequest([Required] string Comment, Guid? EvidenceAttachmentId);

public sealed record RegisterSapPtRequest([Required] string CodigoPT, string? PtName);

public sealed record RegisterSapOrderRequest([Required] string PedidoSapNumber);

public sealed record ResolveSuppliesAgreementRequest([Required] string Text, [Required] Guid EvidenceAttachmentId);

public sealed record RequestInformationRequest([Required] InformationRequestType Type, [Required] string Text);

public sealed record RespondCommercialInformationRequest([Required] string ResponseText);

public sealed record CancelRequest([Required] string Reason);

/// <summary>Rechazos de Diseño: el motivo es obligatorio (es lo que lee Comercial).</summary>
public sealed record RequiredNoteRequest([Required] string Note);

public sealed record RejectArtworkRequest([Required] string Reason, Guid? EvidenceAttachmentId);
