namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>Notificación en-app de sólo consulta — entidad de datos simple, sin comportamiento.</summary>
public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public Guid? NpiId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>Avisos que se agrupan (p. ej. "chat:{npiId}"): mientras haya uno sin leer se actualiza en vez de duplicarse.</summary>
    public string? GroupKey { get; set; }
}
