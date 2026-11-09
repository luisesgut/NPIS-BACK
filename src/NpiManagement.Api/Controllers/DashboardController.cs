using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Dashboard;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;

namespace NpiManagement.Api.Controllers;

/// <summary>Métricas agregadas para los tableros de control (Fase 7, docs/api-contract.md §9). El alcance se limita a los NPIs visibles para el actor — igual que /api/npis.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(GetDashboardSummaryQueryHandler handler, DashboardOverviewQueryHandler overviewHandler) : ControllerBase
{
    /// <summary>
    /// Dashboard por rol (2026-11-09): dirección (AdminTotal/Director, con ?area= para ver un área),
    /// jefe de área (admins de área) o personal (usuarios). El servidor decide la vista con el rol.
    /// months: 1 (mes), 3 (trimestre) o 12 (año); cliente y priority filtran todo.
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> Overview([FromQuery] AreaType? area, [FromQuery] string? cliente, [FromQuery] string? priority, [FromQuery] int months = 1, CancellationToken cancellationToken = default) =>
        Ok(await overviewHandler.HandleAsync(new DashboardOverviewQuery(User.ToCurrentUser(), null, area, new DashboardFilter(cliente, priority, months)), cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        var summary = await handler.HandleAsync(new GetDashboardSummaryQuery(User.ToCurrentUser()), cancellationToken);
        return Ok(summary);
    }
}
