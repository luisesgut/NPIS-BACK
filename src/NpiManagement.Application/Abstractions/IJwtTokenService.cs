using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

/// <summary>Datos mínimos del usuario necesarios para emitir un access token — nunca la entidad Identity completa.</summary>
public sealed record TokenSubject(Guid UserId, string Email, string FullName, string Role, AreaType Area, bool MustChangePassword = false);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Emite access tokens JWT de corta duración. La rotación/almacenamiento de refresh tokens es
/// responsabilidad de IAuthService, no de este servicio (ver docs/architecture-decisions.md).
/// </summary>
public interface IJwtTokenService
{
    AccessToken CreateAccessToken(TokenSubject subject);
}
