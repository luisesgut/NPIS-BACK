using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NpiManagement.Application.Authorization;
using NpiManagement.Domain;

namespace NpiManagement.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Construye el CurrentUser (Application) a partir de los claims del token YA validado —
    /// nunca desde el body de la petición (docs/api-contract.md §12).
    /// </summary>
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("El token no contiene el claim de usuario (sub).");
        var role = principal.FindFirstValue(ClaimTypes.Role)
            ?? throw new InvalidOperationException("El token no contiene el claim de rol.");
        var areaRaw = principal.FindFirstValue("area")
            ?? throw new InvalidOperationException("El token no contiene el claim de área.");

        // Clientes compartidos: los agrega ClientViewClaimsTransformation en cada petición.
        var viewClients = principal.FindAll(NpiManagement.Api.Authorization.ClientViewClaimsTransformation.ClaimType).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var workClients = principal.FindAll(NpiManagement.Api.Authorization.ClientViewClaimsTransformation.WorkClaimType).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new CurrentUser(Guid.Parse(id), role, Enum.Parse<AreaType>(areaRaw), viewClients.Count > 0 ? viewClients : null, workClients.Count > 0 ? workClients : null);
    }

    public static string DisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
}
