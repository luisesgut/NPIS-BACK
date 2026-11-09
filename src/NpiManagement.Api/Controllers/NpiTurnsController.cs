using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;

namespace NpiManagement.Api.Controllers;

/// <summary>"Tu turno": los NPIs que esperan a un área y qué le toca hacer en cada uno (paneles y contador del menú).</summary>
[ApiController]
[Route("api/npis/turns")]
[Authorize]
public sealed class NpiTurnsController(GetMyTurnsQueryHandler handler, NpiSituationResolver situations) : ControllerBase
{
    /// <summary>area sólo aplica a AdminTotal (los demás ven siempre la suya).</summary>
    [HttpGet]
    public Task<IReadOnlyList<TurnItemDto>> Get([FromQuery] AreaType? area, CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetMyTurnsQuery(User.ToCurrentUser(), area), cancellationToken);

    /// <summary>Contadores de los chips "¿Qué necesita?" y "¿Dónde está?" de los paneles (sólo NPIs activos).</summary>
    [HttpGet("situations")]
    public Task<NpiSituationCountsDto> Situations([FromQuery] AreaType? area, CancellationToken cancellationToken) =>
        situations.CountAsync(User.ToCurrentUser(), area, cancellationToken);
}
