namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>
/// Refresh token de sesión. Se guarda únicamente su hash (nunca el valor en texto plano) — ver
/// docs/architecture-decisions.md ADR-019. Entidad de datos simple, sin comportamiento: la
/// lógica de rotación/revocación vive en AuthService (Infrastructure/Services).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? ReasonRevoked { get; set; }
}
