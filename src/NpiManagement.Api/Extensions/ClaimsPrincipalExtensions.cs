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

        return new CurrentUser(Guid.Parse(id), role, Enum.Parse<AreaType>(areaRaw));
    }

    public static string DisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
}
