using System.ComponentModel.DataAnnotations;

namespace NpiManagement.Api.Contracts.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record RefreshRequest([Required] string RefreshToken);

public sealed record LogoutRequest([Required] string RefreshToken);

/// <param name="MustChangePassword">Entró con una contraseña temporal: hasta cambiarla, la API sólo le permite /api/auth.</param>
public sealed record UserResponse(Guid Id, string FullName, string Email, string Role, string Area, bool MustChangePassword = false);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserResponse User);
