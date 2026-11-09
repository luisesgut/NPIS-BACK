namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>Un correo de la libreta de un vendedor para un cliente ("Enviar al cliente" por Outlook) — entidad de datos simple.</summary>
public sealed class ClientRecipient
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Nombre del cliente en mayúsculas (misma llave que la captura).</summary>
    public string Cliente { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>"To" (Para) o "Cc".</summary>
    public string Kind { get; set; } = "To";
    /// <summary>Lo palomeó la última vez: se vuelve a proponer.</summary>
    public bool Selected { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
