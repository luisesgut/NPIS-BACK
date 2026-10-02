using System.ComponentModel.DataAnnotations;
using NpiManagement.Domain;

namespace NpiManagement.Api.Contracts.Users;

/// <summary>
/// Alta solicitada por el propio interesado. Ni el rol ni el estado viajan aquí a propósito: son
/// justo lo que un atacante querría elegir. El área es apenas una sugerencia que el administrador
/// confirma al aprobar.
/// </summary>
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(200)] string FullName,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required] AreaType RequestedArea);

public sealed record ApproveUserRequest(
    [Required] string Role,
    [Required] AreaType Area);

public sealed record SetUserActiveRequest([Required] bool Active);

/// <summary>La contraseña temporal viaja una sola vez, en esta respuesta; el servidor no la conserva en claro.</summary>
public sealed record ResetPasswordResponse(string TemporaryPassword);

public sealed record PendingUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string RequestedArea,
    DateTimeOffset RegisteredAt);
