using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

public enum AuthFailureReason
{
    None = 0,
    InvalidCredentials = 1,
    LockedOut = 2,
    InvalidRefreshToken = 3,
    InactiveUser = 4,

    /// <summary>Se registró pero ningún administrador ha aprobado la cuenta todavía.</summary>
    PendingApproval = 5,
}

/// <summary>Claims propios del sistema, compartidos por quien los emite (Infrastructure) y quien los hace cumplir (Api).</summary>
public static class AuthClaims
{
    /// <summary>
    /// La cuenta tiene una contraseña temporal que puso un administrador y debe cambiarla antes de
    /// usar el sistema. Se guarda como claim de usuario de Identity (tabla AspNetUserClaims, sin
    /// migración) y viaja en el access token para que la API lo haga cumplir.
    /// </summary>
    public const string MustChangePassword = "pwd_change_required";
}

public sealed record AuthenticatedUser(Guid UserId, string Email, string FullName, string Role, AreaType Area, bool MustChangePassword);

public sealed record ChangePasswordResult(bool Succeeded, IReadOnlyList<string> Errors, AuthResult? Session)
{
    public static ChangePasswordResult Fail(params string[] errors) => new(false, errors, null);

    public static ChangePasswordResult Ok(AuthResult session) => new(true, [], session);
}

public sealed record AuthResult(
    bool Succeeded,
    AuthFailureReason FailureReason,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAt,
    AuthenticatedUser? User)
{
    public static AuthResult Failure(AuthFailureReason reason) => new(false, reason, null, null, null, null, null);

    public static AuthResult Success(string accessToken, DateTimeOffset accessExpiresAt, string refreshToken, DateTimeOffset refreshExpiresAt, AuthenticatedUser user) =>
        new(true, AuthFailureReason.None, accessToken, accessExpiresAt, refreshToken, refreshExpiresAt, user);
}

/// <summary>
/// Orquesta login/refresh/logout. Vive detrás de una interfaz en Application, pero su
/// implementación real está en Infrastructure porque depende de ASP.NET Core Identity
/// (UserManager/SignInManager de ApplicationUser, que no puede vivir en Application sin crear
/// una referencia circular hacia Infrastructure) — ver docs/architecture-decisions.md.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Revoca el refresh token indicado. Idempotente: si ya estaba revocado/no existe, no lanza error.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Cambia la contraseña de quien tiene la sesión, verificando la vigente. Quita la marca de
    /// contraseña temporal, cierra las demás sesiones y emite tokens nuevos para ésta.
    /// </summary>
    Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string? ipAddress, CancellationToken cancellationToken);
}
