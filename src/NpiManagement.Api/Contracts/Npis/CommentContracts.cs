using System.ComponentModel.DataAnnotations;
using NpiManagement.Domain;

namespace NpiManagement.Api.Contracts.Npis;

/// <summary>MentionedUserIds: a quiénes se mencionó con @ (deben poder ver el NPI).</summary>
public sealed record AddCommentRequest([Required] string Text, IReadOnlyList<Guid>? MentionedUserIds = null);

/// <summary>Comentario opcional de Comercial al reenviar tras cambios de Ingeniería.</summary>
public sealed record SubmitNpiRequest(string? Comment);

public sealed record AddLinkRequest([Required] string Url, string? Title, AttachmentContext Context = AttachmentContext.Artwork);
