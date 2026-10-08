using NpiManagement.Domain.Common;
using NpiManagement.Domain.Exceptions;

namespace NpiManagement.Domain.Entities;

/// <summary>
/// Mensaje del chat de un NPI (antes "comentario": misma tabla e historial). Vive fuera del
/// agregado <see cref="Npi"/>: escribir en el chat no cambia ningún estado, cronómetro ni hito, así
/// que no necesita cargar el agregado completo ni su RowVersion — y el historial de mensajes no se
/// arrastra en cada acción del flujo. Se puede escribir incluso en estado terminal (registro histórico).
/// </summary>
public sealed class NpiComment
{
    public const int MaxTextLength = 4000;
    public const int MaxMentions = 10;

    public Guid Id { get; }
    public Guid NpiId { get; }
    public Guid AuthorUserId { get; }
    public string AuthorName { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Usuarios mencionados con @ (ya validados por Application: existen y pueden ver el NPI).</summary>
    public List<Guid> MentionedUserIds { get; private set; } = [];

    private NpiComment(Guid id, Guid npiId, Guid authorUserId, string authorName, string text, DateTimeOffset createdAt)
    {
        Id = id;
        NpiId = npiId;
        AuthorUserId = authorUserId;
        AuthorName = authorName;
        Text = text;
        CreatedAt = createdAt;
    }

    public static NpiComment Create(Guid npiId, Actor author, IClock clock, string text, IEnumerable<Guid>? mentionedUserIds = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new NpiValidationException("comment-empty", "El mensaje no puede estar vacío.");

        var trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength)
            throw new NpiValidationException("comment-too-long", $"El mensaje no puede pasar de {MaxTextLength} caracteres.");

        // Mencionarse a sí mismo no avisa a nadie: se descarta en vez de rechazar el mensaje.
        var mentions = (mentionedUserIds ?? []).Where(id => id != author.UserId).Distinct().ToList();
        if (mentions.Count > MaxMentions)
            throw new NpiValidationException("comment-too-many-mentions", $"Se pueden mencionar como máximo {MaxMentions} personas por mensaje.");

        return new NpiComment(Guid.NewGuid(), npiId, author.UserId, author.Name, trimmed, clock.UtcNow)
        {
            MentionedUserIds = mentions,
        };
    }
}
