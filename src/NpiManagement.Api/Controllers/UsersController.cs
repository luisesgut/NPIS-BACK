using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Users;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Consulta de usuarios por área para asignaciones (docs/authorization-matrix.md §3) y
/// administración de cuentas: bandeja de solicitudes pendientes, aprobación, rechazo y baja.
///
/// La consulta la puede hacer cualquier admin; todo lo que **cambia** una cuenta está reservado a
/// AdminTotal. Un AdminComercial no debe poder fabricarse un AdminIngenieria.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = "AnyAdmin")]
public sealed class UsersController(IUserDirectory userDirectory, IUserAdminService userAdminService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AreaType? area, CancellationToken cancellationToken)
    {
        var user = User.ToCurrentUser();
        // Los admins de área sólo consultan su propia área, aun si piden otra por query string
        // (AdminTotal es el único que puede consultar cualquier área) — docs/api-contract.md §2.
        var effectiveArea = user.Role == ApplicationRoles.AdminTotal ? area : user.Area;

        var users = await userDirectory.ListAsync(effectiveArea, cancellationToken);
        return Ok(users);
    }

    /// <summary>Solicitudes de acceso esperando revisión, de la más antigua a la más reciente.</summary>
    [HttpGet("pending")]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> ListPending(CancellationToken cancellationToken)
    {
        var pending = await userAdminService.ListPendingAsync(cancellationToken);

        return Ok(pending.Select(p => new PendingUserResponse(
            p.Id, p.Email, p.FullName, p.RequestedArea.ToString(), p.RegisteredAt)));
    }

    /// <summary>
    /// Aprueba una solicitud asignándole rol y área definitivos. El área debe coincidir con la que
    /// impone el rol; si no, se rechaza con 400 en vez de crear un usuario con permisos ambiguos.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> Approve(Guid id, ApproveUserRequest request, CancellationToken cancellationToken)
    {
        var actor = User.ToCurrentUser();
        await userAdminService.ApproveAsync(id, request.Role, request.Area, actor.UserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var actor = User.ToCurrentUser();
        await userAdminService.RejectAsync(id, actor.UserId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Da de baja o reactiva una cuenta ya aprobada. La baja revoca además sus refresh tokens, de
    /// modo que la sesión abierta deja de renovarse.
    /// </summary>
    [HttpPost("{id:guid}/set-active")]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> SetActive(Guid id, SetUserActiveRequest request, CancellationToken cancellationToken)
    {
        var actor = User.ToCurrentUser();

        // Sin esto, un AdminTotal distraído puede darse de baja a sí mismo y, si es el único,
        // dejar al sistema sin nadie capaz de aprobar o reactivar cuentas.
        if (id == actor.UserId && !request.Active)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "No puedes darte de baja a ti mismo.");

        await userAdminService.SetActiveAsync(id, request.Active, actor.UserId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Asigna una contraseña temporal y la devuelve una sola vez. La cuenta queda obligada a
    /// cambiarla al entrar y sus sesiones abiertas se cierran.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var actor = User.ToCurrentUser();

        // Para la cuenta propia existe "Cambiar contraseña", que pide la vigente: restablecerse a
        // uno mismo permitiría a quien robe una sesión de admin quedarse con la cuenta.
        if (id == actor.UserId)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Para tu propia cuenta usa «Cambiar contraseña».");

        var temporaryPassword = await userAdminService.ResetPasswordAsync(id, actor.UserId, cancellationToken);

        // Una credencial en claro: que ningún proxy ni el navegador guarde esta respuesta.
        Response.Headers.CacheControl = "no-store";
        return Ok(new ResetPasswordResponse(temporaryPassword));
    }
}
