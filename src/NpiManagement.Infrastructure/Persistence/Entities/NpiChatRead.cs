namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>Hasta cuándo leyó un usuario el chat de un NPI — de aquí salen los mensajes sin leer.</summary>
public sealed class NpiChatRead
{
    public Guid NpiId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset LastReadAt { get; set; }
}
