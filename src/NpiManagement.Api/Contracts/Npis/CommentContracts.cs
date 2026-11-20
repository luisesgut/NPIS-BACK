using System.ComponentModel.DataAnnotations;
using NpiManagement.Domain;

namespace NpiManagement.Api.Contracts.Npis;

/// <summary>
/// MentionedUserIds: a quiénes se mencionó con @ (deben poder ver el NPI). AttachmentIds: fotos/archivos ya subidos
/// a /attachments con contexto Comment (2026-11-19); con archivos, el texto puede ir vacío.
/// </summary>
public sealed record AddCommentRequest(string? Text, IReadOnlyList<Guid>? MentionedUserIds = null, IReadOnlyList<Guid>? AttachmentIds = null);

/// <summary>Comentario opcional de Comercial al reenviar tras cambios de Ingeniería.</summary>
public sealed record SubmitNpiRequest(string? Comment);

public sealed record AddLinkRequest([Required] string Url, string? Title, AttachmentContext Context = AttachmentContext.Artwork);
