using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Auth;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Services;

/// <summary>
/// Alta por auto-registro con aprobación administrativa, y administración de cuentas existentes.
/// El registro nunca concede acceso por sí solo: crea la cuenta en <c>Pending</c>, sin rol, y avisa
/// a los administradores. Hasta que alguien la apruebe, el login la rechaza.
/// </summary>
public sealed class UserAdminService(
    UserManager<ApplicationUser> userManager,
    NpiManagementDbContext dbContext,
    INotificationService notificationService,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    IClock clock,
    IRealtimeEvents realtime,
    ILogger<UserAdminService> logger) : IUserAdminService
{
    /// <summary>
    /// Sin esta restricción el endpoint de registro queda abierto de par en par: cualquiera en
    /// internet podría crear cuentas y llenar la bandeja de pendientes. Configurable por si el
    /// dominio cambia, pero nunca vacío.
    /// </summary>
    private string AllowedDomain =>
        configuration["Registration:AllowedEmailDomain"] is { Length: > 0 } configured
            ? configured
            : "bioflex.mx";

    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        if (!email.EndsWith("@" + AllowedDomain, StringComparison.OrdinalIgnoreCase))
            return RegistrationResult.Fail(RegistrationOutcome.DomainNotAllowed, $"El correo debe pertenecer al dominio {AllowedDomain}.");

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            // No es un error para el llamador: el controlador responde 202 igual que en el caso
            // bueno. Sólo queda constancia en el log del servidor.
            logger.LogInformation("Registro descartado: el correo ya tiene cuenta.");
            return RegistrationResult.Fail(RegistrationOutcome.EmailAlreadyExists);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = request.FullName.Trim(),
            Area = request.RequestedArea,
            Status = UserStatus.Pending,
            RegisteredAt = clock.UtcNow,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            // Errores de política de contraseña de Identity: éstos sí se devuelven al usuario,
            // porque hablan de lo que él escribió y no revelan nada sobre otras cuentas.
            var errors = IdentityErrorMessages.Translate(created.Errors);
            return RegistrationResult.Fail(RegistrationOutcome.PasswordRejected, errors);
        }

        // Deliberadamente NO se asigna ningún rol aquí: un usuario sin rol no puede obtener un
        // token (IssueTokensAsync lo rechaza), así que aunque algo activara la cuenta por error,
        // seguiría sin poder entrar. El rol lo pone el administrador al aprobar.
        await NotifyAdminsAsync(
            $"Nueva solicitud de acceso: {user.FullName} ({user.Email}) — área solicitada: {request.RequestedArea}.",
            cancellationToken);

        // La bandeja de pendientes de los admins se actualiza sola.
        await realtime.UsersChangedAsync();
        logger.LogInformation("Solicitud de acceso creada para el usuario {UserId}, pendiente de aprobación.", user.Id);
        return RegistrationResult.Ok();
    }

    public async Task<IReadOnlyList<PendingUserDto>> ListPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Status == UserStatus.Pending)
            .OrderBy(u => u.RegisteredAt)
            .Select(u => new PendingUserDto(u.Id, u.Email ?? string.Empty, u.FullName, u.Area, u.RegisteredAt))
            .ToListAsync(cancellationToken);

        return pending;
    }

    public async Task ApproveAsync(Guid userId, string role, AreaType area, Guid approvedByUserId, CancellationToken cancellationToken)
    {
        var user = await FindOrThrowAsync(userId);

        if (user.Status != UserStatus.Pending)
            throw new RequestValidationException("status", "Esta cuenta no está pendiente de aprobación.");

        if (!ApplicationRoles.Exists(role))
            throw new RequestValidationException("role", $"El rol '{role}' no existe.");

        // El área no se toma tal cual viene: el rol la determina. Un AdminComercial con área
        // Diseño produciría permisos impredecibles en todo el workflow.
        var areaForRole = ApplicationRoles.AreaFor(role)!.Value;
        if (area != areaForRole)
            throw new RequestValidationException("area", $"El rol '{role}' corresponde al área {areaForRole}, no a {area}.");

        user.Area = areaForRole;
        user.Status = UserStatus.Active;
        user.EmailConfirmed = true;
        user.ApprovedByUserId = approvedByUserId;
        user.ApprovedAt = clock.UtcNow;

        // Un usuario tiene exactamente un rol (IssueTokensAsync toma el primero). Se limpian los
        // que hubiera para que reaprobar una cuenta no acumule roles contradictorios.
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, currentRoles));

        ThrowIfFailed(await userManager.AddToRoleAsync(user, role));
        ThrowIfFailed(await userManager.UpdateAsync(user));

        notificationService.Notify(user.Id, $"Tu acceso fue aprobado con el rol {role}. Ya puedes iniciar sesión.", null, NotificationChannel.BellAndEmail);
        // Por IUnitOfWork y no dbContext directo: es el único punto que, tras confirmar, entrega a
        // la cola de correo los avisos BellAndEmail. Con SaveChanges directo la campana sí se
        // guardaba, pero el correo se descartaba en silencio.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await realtime.UsersChangedAsync();
        logger.LogInformation("Usuario {UserId} aprobado con rol {Role} por {ApprovedBy}.", user.Id, role, approvedByUserId);
    }

    public async Task RejectAsync(Guid userId, Guid rejectedByUserId, CancellationToken cancellationToken)
    {
        var user = await FindOrThrowAsync(userId);

        if (user.Status != UserStatus.Pending)
            throw new RequestValidationException("status", "Esta cuenta no está pendiente de aprobación.");

        user.Status = UserStatus.Disabled;
        ThrowIfFailed(await userManager.UpdateAsync(user));

        await realtime.UsersChangedAsync();
        logger.LogInformation("Solicitud del usuario {UserId} rechazada por {RejectedBy}.", user.Id, rejectedByUserId);
    }

    public async Task SetActiveAsync(Guid userId, bool active, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var user = await FindOrThrowAsync(userId);

        if (user.Status == UserStatus.Pending)
            throw new RequestValidationException("status", "Esta cuenta sigue pendiente de aprobación; apruébala o recházala.");

        if (active && (await userManager.GetRolesAsync(user)).Count == 0)
            throw new RequestValidationException("role", "Esta cuenta no tiene rol asignado; no puede reactivarse.");

        user.Status = active ? UserStatus.Active : UserStatus.Disabled;
        ThrowIfFailed(await userManager.UpdateAsync(user));

        if (!active)
            await RevokeAllRefreshTokensAsync(user.Id, $"Cuenta dada de baja por {changedByUserId}", cancellationToken);

        await realtime.UsersChangedAsync();
        logger.LogInformation("Usuario {UserId} {Accion} por {ChangedBy}.", user.Id, active ? "reactivado" : "dado de baja", changedByUserId);
    }

    public async Task<string> ResetPasswordAsync(Guid userId, Guid resetByUserId, CancellationToken cancellationToken)
    {
        var user = await FindOrThrowAsync(userId);

        if (user.Status != UserStatus.Active)
            throw new RequestValidationException("status", "Sólo se puede restablecer la contraseña de una cuenta activa.");

        var temporaryPassword = TemporaryPasswordGenerator.Create();
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        ThrowIfFailed(await userManager.ResetPasswordAsync(user, resetToken, temporaryPassword));

        // Quien pide ayuda con su contraseña casi siempre ya se bloqueó a sí mismo con intentos fallidos.
        ThrowIfFailed(await userManager.SetLockoutEndDateAsync(user, null));
        ThrowIfFailed(await userManager.ResetAccessFailedCountAsync(user));

        if (!(await userManager.GetClaimsAsync(user)).Any(c => c.Type == AuthClaims.MustChangePassword))
            ThrowIfFailed(await userManager.AddClaimAsync(user, new Claim(AuthClaims.MustChangePassword, "true")));

        // La contraseña anterior pudo conocerla alguien más: ninguna sesión abierta con ella sobrevive.
        await RevokeAllRefreshTokensAsync(user.Id, $"Contraseña restablecida por {resetByUserId}", cancellationToken);

        // Aviso de seguridad, nunca con la contraseña: si la persona no lo pidió, se entera.
        notificationService.Notify(
            user.Id,
            "Un administrador restableció tu contraseña. Entra con la contraseña temporal que te compartió; el sistema te pedirá cambiarla. Si tú no lo pediste, avisa al administrador.",
            null,
            NotificationChannel.BellAndEmail);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Contraseña del usuario {UserId} restablecida por {ResetBy}.", user.Id, resetByUserId);
        return temporaryPassword;
    }

    /// <summary>
    /// Dar de baja sin esto dejaría al usuario operando con normalidad hasta que su refresh token
    /// expirara por su cuenta (días). El access token que ya tenga en memoria sigue siendo válido
    /// hasta que caduque — por eso conviene que su vida sea corta.
    /// </summary>
    private async Task RevokeAllRefreshTokensAsync(Guid userId, string reason, CancellationToken cancellationToken)
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

    private async Task NotifyAdminsAsync(string message, CancellationToken cancellationToken)
    {
        var admins = await userManager.GetUsersInRoleAsync(ApplicationRoles.AdminTotal);
        var recipients = admins.Where(a => a.Status == UserStatus.Active).Select(a => a.Id).ToList();

        if (recipients.Count == 0)
        {
            logger.LogWarning("No hay ningún AdminTotal activo que pueda atender la solicitud de acceso.");
            return;
        }

        notificationService.NotifyMany(recipients, message, null, NotificationChannel.BellAndEmail);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ApplicationUser> FindOrThrowAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new NotFoundException("Usuario", userId);

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new RequestValidationException("identity", string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
