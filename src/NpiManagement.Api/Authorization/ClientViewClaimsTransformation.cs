using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using NpiManagement.Application.Users;
using NpiManagement.Domain;

namespace NpiManagement.Api.Authorization;

/// <summary>
/// Agrega a la identidad los clientes que el usuario puede consultar ("Clientes que puede ver",
/// 2026-11-10). Van como claims de la petición y no en el token: así un cambio de AdminTotal aplica
/// al momento (con un minuto de caché), sin que la persona tenga que volver a entrar.
/// </summary>
public sealed class ClientViewClaimsTransformation(IClientViewGrantStore grants) : IClaimsTransformation
{
    public const string ClaimType = "view_client";
    /// <summary>"Ver y trabajar": clientes cuyos NPIs además puede operar como el equipo de su área.</summary>
    public const string WorkClaimType = "work_client";
    private const string LoadedMarker = "view_clients_loaded";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Puede llamarse más de una vez por petición; los admins ya ven todo.
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(c => c.Type == LoadedMarker)) return principal;
        var role = principal.FindFirstValue(ClaimTypes.Role);
        var idRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (role is not (ApplicationRoles.UsuarioComercial or ApplicationRoles.UsuarioIngenieria or ApplicationRoles.UsuarioDiseno)
            || !Guid.TryParse(idRaw, out var userId))
            return principal;

        var clients = await grants.GetGrantsAsync(userId, CancellationToken.None);
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(LoadedMarker, "1"));
        foreach (var cliente in clients.View) identity.AddClaim(new Claim(ClaimType, cliente));
        foreach (var cliente in clients.Work) identity.AddClaim(new Claim(WorkClaimType, cliente));
        principal.AddIdentity(identity);
        return principal;
    }
}
