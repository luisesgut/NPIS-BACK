using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NpiManagement.Api.Contracts.Auth;
using NpiManagement.Api.Contracts.Users;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Login, registro, refresh y logout. El registro (ADR-019, revisado) es de auto-servicio pero
/// **no** concede acceso: crea una solicitud en estado Pending, sin rol, que un AdminTotal debe
/// aprobar desde /api/users/pending. Controller delgado: la lógica vive en IAuthService y
/// IUserAdminService.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IUserAdminService userAdminService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(ToResponse(result)) : ToProblem(result.FailureReason);
    }

    /// <summary>
    /// Solicitud de alta. Responde 202 tanto si se creó la cuenta como si el correo ya tenía una:
    /// distinguir ambos casos permitiría averiguar quién trabaja en la empresa probando
    /// direcciones. Los únicos rechazos visibles son los que hablan de lo que el propio
    /// solicitante escribió — dominio equivocado o contraseña que no cumple la política.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await userAdminService.RegisterAsync(
            new RegistrationRequest(request.Email, request.FullName, request.Password, request.RequestedArea),
            cancellationToken);

        return result.Outcome switch
        {
            RegistrationOutcome.Created or RegistrationOutcome.EmailAlreadyExists =>
                Accepted(new { message = "Solicitud recibida. Un administrador debe aprobarla antes de que puedas iniciar sesión." }),

            RegistrationOutcome.DomainNotAllowed =>
                Problem(statusCode: StatusCodes.Status400BadRequest, title: string.Join(" ", result.Errors), type: "registration:domain-not-allowed"),

            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: string.Join(" ", result.Errors), type: "registration:password-rejected"),
        };
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(ToResponse(result)) : ToProblem(result.FailureReason);
    }

    [HttpPost("logout")]
    [AllowAnonymous] // revocar un refresh token no requiere que el access token siga vigente
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserResponse> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var name = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var area = User.FindFirstValue("area") ?? string.Empty;
        var mustChangePassword = User.HasClaim(c => c.Type == AuthClaims.MustChangePassword);

        return Ok(new UserResponse(Guid.Parse(id!), name, email, role, area, mustChangePassword));
    }

    /// <summary>
    /// Cambia la contraseña de quien tiene la sesión. Responde con tokens nuevos: el access token
    /// anterior podía traer la marca de contraseña temporal, que aquí se retira.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var result = await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ClientIp(), cancellationToken);

        return result.Succeeded
            ? Ok(ToResponse(result.Session!))
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: string.Join(" ", result.Errors), type: "password-change-rejected");
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private static AuthResponse ToResponse(AuthResult result) => new(
        result.AccessToken!,
        result.AccessTokenExpiresAt!.Value,
        result.RefreshToken!,
        result.RefreshTokenExpiresAt!.Value,
        new UserResponse(result.User!.UserId, result.User.FullName, result.User.Email, result.User.Role, result.User.Area.ToString(), result.User.MustChangePassword));

    private ObjectResult ToProblem(AuthFailureReason reason)
    {
        var (status, title) = reason switch
        {
            AuthFailureReason.LockedOut => (StatusCodes.Status401Unauthorized, "Cuenta bloqueada temporalmente por demasiados intentos fallidos."),
            AuthFailureReason.InvalidRefreshToken => (StatusCodes.Status401Unauthorized, "Refresh token inválido, expirado o revocado."),
            AuthFailureReason.PendingApproval => (StatusCodes.Status403Forbidden, "Tu solicitud de acceso sigue pendiente de aprobación por un administrador."),
            AuthFailureReason.InactiveUser => (StatusCodes.Status403Forbidden, "Esta cuenta está dada de baja. Contacta a un administrador."),
            _ => (StatusCodes.Status401Unauthorized, "Credenciales inválidas."),
        };

        return Problem(statusCode: status, title: title, type: $"auth-failure:{reason}");
    }
}
