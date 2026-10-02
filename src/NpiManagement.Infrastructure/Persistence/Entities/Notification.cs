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
}
