namespace NpiManagement.Domain.Entities;

/// <summary>Comentario libre sobre un NPI. Se puede agregar incluso en estado terminal (registro histórico).</summary>
public sealed class NpiComment
{
    public Guid Id { get; }
    public Guid AuthorUserId { get; }
    public string AuthorName { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }

    internal NpiComment(Guid id, Guid authorUserId, string authorName, string text, DateTimeOffset createdAt)
    {
        Id = id;
        AuthorUserId = authorUserId;
        AuthorName = authorName;
        Text = text;
        CreatedAt = createdAt;
    }
}
