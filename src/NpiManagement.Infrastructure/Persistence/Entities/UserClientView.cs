namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>Un cliente cuyos NPIs puede consultar un usuario aunque no sean suyos (ver IClientViewGrantStore).</summary>
public sealed class UserClientView
{
    public Guid UserId { get; set; }

    /// <summary>Nombre del cliente normalizado (MAYÚSCULAS, espacios simples), igual que en la captura.</summary>
    public string Cliente { get; set; } = string.Empty;

    /// <summary>"Ver y trabajar" (2026-11-10): además de consultar, actúa en esos NPIs como el equipo de su área.</summary>
    public bool CanWork { get; set; }

    public Guid GrantedByUserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}
