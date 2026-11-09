namespace NpiManagement.Application.Npis.Dtos;

/// <summary>
/// Cronómetro calculado en el servidor. ElapsedMs es el tiempo hábil hasta el momento de la respuesta.
/// IsTicking: en marcha y en horario hábil ahora mismo (sólo así el front suma segundos).
/// MsUntilChange: cuánto falta para que IsTicking cambie (cierre o apertura de jornada); el front
/// deja de sumar ahí y vuelve a pedir el dato. Son duraciones relativas a propósito: así no importa
/// si el reloj de cada computadora está adelantado o atrasado.
/// </summary>
public sealed record TimerDto(bool HasStarted, bool IsRunning, bool IsClosed, long ElapsedMs, bool IsTicking = false, long? MsUntilChange = null);

/// <summary>Mensaje del chat del NPI (antes "comentario"). MentionedUserIds: a quiénes se mencionó con @.</summary>
public sealed record CommentDto(Guid Id, Guid NpiId, Guid AuthorUserId, string AuthorName, string Text, DateTimeOffset CreatedAt, IReadOnlyList<Guid> MentionedUserIds);

public sealed record FieldChangeDto(string Field, string Before, string After);

/// <summary>ResolvedAt/ChangesApplied: cuándo y qué corrigió Comercial al reenviar (solicitudes de Ingeniería o Diseño). EvidenceAttachmentId: apoyo opcional (artes rechazadas).</summary>
public sealed record ChangeRequestDto(
    Guid Id, Guid RequestedByUserId, string RequestedByName, DateTimeOffset RequestedAt, string FromArea, string ToArea, string Origin,
    string Description, IReadOnlyList<string> FieldKeys,
    DateTimeOffset? ResolvedAt, string? ResolvedByName, string? ResponseComment, IReadOnlyList<FieldChangeDto> ChangesApplied, Guid? EvidenceAttachmentId,
    IReadOnlyList<ChecklistItemDto> Checklist,
    /// <summary>Comentario por campo marcado (clave del campo → qué corregir).</summary>
    IReadOnlyDictionary<string, string>? FieldNotes = null,
    /// <summary>Capturas o fotos adjuntas al pedir los cambios (ids de adjuntos del NPI).</summary>
    IReadOnlyList<Guid>? ImageAttachmentIds = null,
    /// <summary>Si quien la pidió la retiró antes de que Comercial la atendiera.</summary>
    DateTimeOffset? WithdrawnAt = null);

/// <summary>Un cambio que pidió el cliente sobre el PrintCard; DoneAt != null cuando Diseño ya lo palomeó.</summary>
public sealed record ChecklistItemDto(Guid Id, string Text, DateTimeOffset? DoneAt, string? DoneByName, string? Comment);

public sealed record InformationRequestDto(Guid Id, string Type, string Status, string Text, Guid RequestedByUserId, string RequestedByName, DateTimeOffset RequestedAt, DateTimeOffset? ResolvedAt, Guid? ResolvedByUserId, string? ResponseText, IReadOnlyList<Guid>? ImageAttachmentIds = null);

public sealed record FeasibilityReviewDto(Guid Id, string Area, Guid ApprovedByUserId, string ApprovedByName, DateTimeOffset ApprovedAt, string? Note, IReadOnlyList<string> ConfirmedFieldKeys);

public sealed record PrintCardApprovalDto(Guid Id, string Role, Guid ByUserId, string ByName, DateTimeOffset At, string? Note, Guid? EvidenceAttachmentId);

public sealed record PrintCardVersionDto(Guid Id, int VersionNumber, string Code, Guid? AttachmentId, Guid UploadedByUserId, string UploadedByName, DateTimeOffset UploadedAt, string? Note, IReadOnlyList<PrintCardApprovalDto> Approvals,
    DateTimeOffset? RejectedAt, string? RejectedByName, string? RejectionReason, Guid? RejectionEvidenceAttachmentId,
    /// <summary>Puntos que Diseño pidió a Comercial revisar con el cliente; Comercial los palomea.</summary>
    IReadOnlyList<ChecklistItemDto>? CommercialChecklist = null);

/// <summary>Url != null: el adjunto es un link externo y se abre directamente, no se descarga.</summary>
public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string Context, int Version, Guid UploadedByUserId, DateTimeOffset UploadedAt, string? Url);

public sealed record TimerEventDto(Guid Id, string Stage, string Action, DateTimeOffset OccurredAt, Guid? ActorUserId, long? DurationMsAtEvent, string? FromStage, string? ToStage, string? Note);

public sealed record AuditEventDto(Guid Id, Guid ActorUserId, string ActorRole, DateTimeOffset OccurredAt, string Action, string? Details);

/// <summary>
/// Cotización de SISPRO ligada al NPI. Snapshot: lo que dijo SISPRO al ligarla (null si la foto ya no
/// se puede leer). Quien no es de Comercial la recibe sin precios (QuotePriceRedaction).
/// </summary>
public sealed record SisproQuoteDto(
    Guid Id, int VersionNumber, string NumeroCotizacion, string EstadoSispro, string Status,
    Guid LinkedByUserId, string LinkedByName, DateTimeOffset LinkedAt, IReadOnlyList<string> StructureWarnings,
    DateTimeOffset? RespondedAt, string? RespondedByName, string? ResponseNote, Guid? EvidenceAttachmentId,
    DateTimeOffset? InvalidatedAt, string? InvalidationReason,
    Abstractions.QuotationSnapshot? Snapshot);

/// <summary>
/// Propuesta de cierre de un usuario normal y la decisión del admin. Reason es el TerminalReason que
/// se propone; Status: Pending, Confirmed, Declined, Withdrawn o Superseded.
/// </summary>
public sealed record ClosureProposalDto(
    Guid Id, string Reason, string Area, string Status,
    Guid ProposedByUserId, string ProposedByName, DateTimeOffset ProposedAt, string Note, Guid? EvidenceAttachmentId,
    string? DecidedByName, DateTimeOffset? DecidedAt, string? DecisionNote);

/// <summary>Detalle completo del NPI — ver docs/api-contract.md §3.3.</summary>
public sealed record NpiDetailDto(
    Guid Id,
    byte[] RowVersion,
    string? NpiNumber,
    string Priority,
    NpiCaptureDto Capture,
    string LifecycleStatus,
    string? TerminalReason,
    string EngineeringAreaStatus,
    string DesignAreaStatus,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateOnly RequestDate,
    Guid? AssignedEngineeringUserId,
    Guid? AssignedDesignUserId,
    Guid AssignedCommercialUserId,
    TimerDto GlobalTimer,
    TimerDto CommercialTimer,
    TimerDto EngineeringTimer,
    TimerDto DesignTimer,
    DateTimeOffset? ArtesValidadasAt,
    string? ArtClassification,
    bool? HasLabValues,
    DateTimeOffset? PrintCardSentToClientAt,
    DateTimeOffset? PrintCardLoadedAt,
    string? CodigoPT,
    string? PtName,
    DateTimeOffset? PtRegisteredAt,
    string? PedidoSapNumber,
    DateTimeOffset? PedidoSapRegisteredAt,
    DateTimeOffset? EtCreatedAt,
    DateTimeOffset? EtLoadedInSisproAt,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<ChangeRequestDto> ChangeRequests,
    IReadOnlyList<InformationRequestDto> InformationRequests,
    IReadOnlyList<FeasibilityReviewDto> FeasibilityReviews,
    IReadOnlyList<PrintCardVersionDto> PrintCardVersions,
    IReadOnlyList<SisproQuoteDto> SisproQuotes,
    IReadOnlyList<ClosureProposalDto> ClosureProposals,
    /// <summary>La última acción, si todavía se puede deshacer (sólo la puede deshacer ByUserId).</summary>
    UndoableActionDto? UndoableAction = null,
    /// <summary>Por qué se cerró (rechazo o cancelación): el motivo escrito, quién (su rol) y cuándo. null si sigue abierto.</summary>
    ClosingInfoDto? Closing = null,
    /// <summary>Indicaciones del admin de Diseño para el diseñador.</summary>
    string? DesignInstructions = null,
    /// <summary>Tabla copiada de Excel/SAP al cargar el PT (separada por tabuladores).</summary>
    string? PtSapTable = null,
    /// <summary>Id de la versión final del PrintCard (la cargada en sistema); null mientras no hay.</summary>
    Guid? PrintCardFinalVersionId = null);

/// <summary>Cierre del NPI: Note es el motivo que se escribió (puede faltar si el admin cerró sin nota); ByRole, el rol de quien lo cerró.</summary>
public sealed record ClosingInfoDto(string? Note, string ByRole, DateTimeOffset At);

/// <summary>Acción que se puede deshacer: el evento de bitácora (p. ej. "PrintCardApproved") y quién la hizo.</summary>
public sealed record UndoableActionDto(string Action, string? Details, Guid ByUserId, DateTimeOffset At);
