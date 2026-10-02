using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;
using RefreshTokenEntity = NpiManagement.Infrastructure.Persistence.Entities.RefreshToken;

namespace NpiManagement.Infrastructure.Auth;

/// <summary>
/// Implementación real de IAuthService (ver justificación de la interfaz en Application).
/// Login usa SignInManager.CheckPasswordSignInAsync con lockoutOnFailure:true, que aplica el
/// bloqueo por intentos fallidos ya configurado en Identity (Fase 2: 5 intentos, 15 min).
/// Refresh implementa rotación: cada uso revoca el token presentado y emite uno nuevo; si se
/// reutiliza un token ya revocado (indicio de robo), se revocan TODAS las sesiones del usuario.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    NpiManagementDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions,
    IClock clock) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthResult> LoginAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return AuthResult.Failure(AuthFailureReason.InvalidCredentials);

        // La contraseña se verifica ANTES de revelar por qué la cuenta no sirve: de lo contrario
        // cualquiera podría distinguir "existe pero está pendiente" de "no existe" sin conocer la
        // contraseña, y eso ya es enumerar usuarios. Sólo quien acierta merece el motivo real.

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
            return AuthResult.Failure(AuthFailureReason.LockedOut);
        if (!signInResult.Succeeded)
            return AuthResult.Failure(AuthFailureReason.InvalidCredentials);

        if (user.Status != UserStatus.Active)
        {
            return AuthResult.Failure(user.Status == UserStatus.Pending
                ? AuthFailureReason.PendingApproval
                : AuthFailureReason.InactiveUser);
        }

        return await IssueTokensAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var hash = RefreshTokenGenerator.Hash(refreshToken);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
            return AuthResult.Failure(AuthFailureReason.InvalidRefreshToken);

        var now = clock.UtcNow;
        if (stored.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(stored.UserId, "Reuso de refresh token ya revocado (posible robo)", cancellationToken);
            return AuthResult.Failure(AuthFailureReason.InvalidRefreshToken);
        }

        if (stored.ExpiresAt <= now)
            return AuthResult.Failure(AuthFailureReason.InvalidRefreshToken);

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active)
            return AuthResult.Failure(AuthFailureReason.InvalidRefreshToken);

        return await IssueTokensAsync(user, ipAddress, cancellationToken, replacing: stored);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = RefreshTokenGenerator.Hash(refreshToken);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null || stored.RevokedAt is not null) return;

        stored.RevokedAt = clock.UtcNow;
        stored.ReasonRevoked = "Logout";
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
            return ChangePasswordResult.Fail("Esta cuenta no está activa.");

        if (currentPassword == newPassword)
            return ChangePasswordResult.Fail("La nueva contraseña debe ser distinta de la actual.");

        var changed = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changed.Succeeded)
            return ChangePasswordResult.Fail(IdentityErrorMessages.Translate(changed.Errors));

        var pendingChange = (await userManager.GetClaimsAsync(user))
            .Where(c => c.Type == AuthClaims.MustChangePassword)
            .ToList();
        if (pendingChange.Count > 0)
            await userManager.RemoveClaimsAsync(user, pendingChange);

        // Quien cambia su contraseña puede estar respondiendo a la sospecha de que alguien más la
        // conocía: se cierran todas las sesiones y ésta sigue con tokens nuevos (el access token
        // anterior todavía decía que había que cambiarla).
        await RevokeAllForUserAsync(user.Id, "Cambio de contraseña", cancellationToken);
        var session = await IssueTokensAsync(user, ipAddress, cancellationToken);

        return session.Succeeded
            ? ChangePasswordResult.Ok(session)
            : ChangePasswordResult.Fail("La contraseña se cambió, pero no se pudo abrir la sesión. Inicia sesión de nuevo.");
    }

    private async Task<AuthResult> IssueTokensAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken, RefreshTokenEntity? replacing = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault();
        if (role is null)
            return AuthResult.Failure(AuthFailureReason.InvalidCredentials);

        var mustChangePassword = (await userManager.GetClaimsAsync(user)).Any(c => c.Type == AuthClaims.MustChangePassword);
        var subject = new TokenSubject(user.Id, user.Email!, user.FullName, role, user.Area, mustChangePassword);
        var accessToken = jwtTokenService.CreateAccessToken(subject);

        var now = clock.UtcNow;
        var rawRefreshToken = RefreshTokenGenerator.CreateToken();
        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = RefreshTokenGenerator.Hash(rawRefreshToken),
            CreatedAt = now,
            CreatedByIp = ipAddress,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays),
        };
        dbContext.RefreshTokens.Add(refreshTokenEntity);

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReasonRevoked = "Rotado";
            replacing.ReplacedByTokenId = refreshTokenEntity.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var authUser = new AuthenticatedUser(user.Id, user.Email!, user.FullName, role, user.Area, mustChangePassword);
        return AuthResult.Success(accessToken.Value, accessToken.ExpiresAt, rawRefreshToken, refreshTokenEntity.ExpiresAt, authUser);
    }

    private async Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var active = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.ReasonRevoked = reason;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
