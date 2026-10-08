namespace NpiManagement.Application.Npis.Dtos;

public sealed record TimerDto(bool HasStarted, bool IsRunning, bool IsClosed, long ElapsedMs);

/// <summary>Mensaje del chat del NPI (antes "comentario"). MentionedUserIds: a quiénes se mencionó con @.</summary>
public sealed record CommentDto(Guid Id, Guid NpiId, Guid AuthorUserId, string AuthorName, string Text, DateTimeOffset CreatedAt, IReadOnlyList<Guid> MentionedUserIds);

public sealed record FieldChangeDto(string Field, string Before, string After);

/// <summary>ResolvedAt/ChangesApplied: cuándo y qué corrigió Comercial al reenviar (sólo solicitudes de Ingeniería).</summary>
public sealed record ChangeRequestDto(
    Guid Id, Guid RequestedByUserId, string RequestedByName, DateTimeOffset RequestedAt, string FromArea, string ToArea, string Origin,
    string Description, IReadOnlyList<string> FieldKeys,
    DateTimeOffset? ResolvedAt, string? ResolvedByName, string? ResponseComment, IReadOnlyList<FieldChangeDto> ChangesApplied);

public sealed record InformationRequestDto(Guid Id, string Type, string Status, string Text, Guid RequestedByUserId, string RequestedByName, DateTimeOffset RequestedAt, DateTimeOffset? ResolvedAt, Guid? ResolvedByUserId, string? ResponseText);

public sealed record FeasibilityReviewDto(Guid Id, string Area, Guid ApprovedByUserId, string ApprovedByName, DateTimeOffset ApprovedAt, string? Note, IReadOnlyList<string> ConfirmedFieldKeys);

public sealed record PrintCardApprovalDto(Guid Id, string Role, Guid ByUserId, string ByName, DateTimeOffset At, string? Note, Guid? EvidenceAttachmentId);

public sealed record PrintCardVersionDto(Guid Id, int VersionNumber, string Code, Guid? AttachmentId, Guid UploadedByUserId, string UploadedByName, DateTimeOffset UploadedAt, string? Note, IReadOnlyList<PrintCardApprovalDto> Approvals);

/// <summary>Url != null: el adjunto es un link externo y se abre directamente, no se descarga.</summary>
public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string Context, int Version, Guid UploadedByUserId, DateTimeOffset UploadedAt, string? Url);

public sealed record TimerEventDto(Guid Id, string Stage, string Action, DateTimeOffset OccurredAt, Guid? ActorUserId, long? DurationMsAtEvent, string? FromStage, string? ToStage, string? Note);

public sealed record AuditEventDto(Guid Id, Guid ActorUserId, string ActorRole, DateTimeOffset OccurredAt, string Action, string? Details);

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
    IReadOnlyList<PrintCardVersionDto> PrintCardVersions);
