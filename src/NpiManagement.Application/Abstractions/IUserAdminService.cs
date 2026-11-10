using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

/// <summary>Solicitud de alta hecha por el propio interesado desde la pantalla de login.</summary>
public sealed record RegistrationRequest(string Email, string FullName, string Password, AreaType RequestedArea);

/// <summary>
/// Resultado interno del registro. Ojo: el controlador **nunca** lo traduce a respuestas
/// distintas — siempre devuelve 202. Distinguir "correo ya registrado" de "alta creada" le
/// permitiría a cualquiera averiguar quién trabaja en Bioflex probando direcciones. El detalle
/// existe sólo para el log del servidor.
/// </summary>
public enum RegistrationOutcome
{
    Created = 0,
    EmailAlreadyExists = 1,
    DomainNotAllowed = 2,
    PasswordRejected = 3,
}

public sealed record RegistrationResult(RegistrationOutcome Outcome, IReadOnlyList<string> Errors)
{
    public static RegistrationResult Ok() => new(RegistrationOutcome.Created, []);
    public static RegistrationResult Fail(RegistrationOutcome outcome, params string[] errors) => new(outcome, errors);
}

public sealed record PendingUserDto(Guid Id, string Email, string FullName, AreaType RequestedArea, DateTimeOffset RegisteredAt);

/// <summary>
/// Alta y administración de cuentas. Vive detrás de una interfaz en Application pero se implementa
/// en Infrastructure por la misma razón que <see cref="IAuthService"/>: depende de UserManager de
/// ASP.NET Identity, que no puede referenciarse desde Application sin invertir la dirección de las
/// dependencias.
/// </summary>
public interface IUserAdminService
{
    /// <summary>
    /// Crea una cuenta en estado <c>Pending</c>, sin rol. Valida que el correo pertenezca al
    /// dominio corporativo configurado y notifica a los administradores. Nunca inicia sesión.
    /// </summary>
    Task<RegistrationResult> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingUserDto>> ListPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Aprueba una solicitud: asigna rol y área definitivos y activa la cuenta. El área debe ser
    /// coherente con el rol (<see cref="ApplicationRoles.AreaFor"/>) o se rechaza.
    /// </summary>
    Task ApproveAsync(Guid userId, string role, AreaType area, Guid approvedByUserId, CancellationToken cancellationToken);

    /// <summary>Rechaza una solicitud pendiente: la cuenta queda <c>Disabled</c> y no puede entrar.</summary>
    Task RejectAsync(Guid userId, Guid rejectedByUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Da de baja o reactiva una cuenta ya aprobada. Al darla de baja se revocan todos sus refresh
    /// tokens: sin eso el usuario conservaría la sesión abierta hasta que expirara por su cuenta.
    /// </summary>
    Task SetActiveAsync(Guid userId, bool active, Guid changedByUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Asigna una contraseña temporal a una cuenta activa y la devuelve para que el administrador
    /// la comparta: no se guarda en claro ni se envía por correo. Desbloquea la cuenta, cierra sus
    /// sesiones y la obliga a cambiar la contraseña al entrar.
    /// </summary>
    Task<string> ResetPasswordAsync(Guid userId, Guid resetByUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Cambia el rol (y con él el área) de una cuenta ya aprobada (2026-11-10). Cierra sus sesiones
    /// para que el nuevo rol aplique al volver a entrar. No permite dejar al sistema sin AdminTotal activo.
    /// </summary>
    Task ChangeRoleAsync(Guid userId, string role, Guid changedByUserId, CancellationToken cancellationToken);
}
