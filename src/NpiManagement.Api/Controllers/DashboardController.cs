using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Queries;

namespace NpiManagement.Api.Controllers;

/// <summary>Métricas agregadas para los tableros de control (Fase 7, docs/api-contract.md §9). El alcance se limita a los NPIs visibles para el actor — igual que /api/npis.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(GetDashboardSummaryQueryHandler handler) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        var summary = await handler.HandleAsync(new GetDashboardSummaryQuery(User.ToCurrentUser()), cancellationToken);
        return Ok(summary);
    }
}
