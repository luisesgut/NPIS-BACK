using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Users;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// "Mi equipo" de Comercial (2026-11-18): el director ve a sus vendedores; el vendedor, a su director.
/// Sólo dice quiénes son: los NPIs salen del listado con <c>team=true</c>.
/// </summary>
[ApiController]
[Route("api/team")]
[Authorize]
public sealed class TeamController(ICommercialTeamStore teams) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MyTeamDto>> Me(CancellationToken cancellationToken) =>
        Ok(await teams.GetMyTeamAsync(User.ToCurrentUser().UserId, cancellationToken));
}
