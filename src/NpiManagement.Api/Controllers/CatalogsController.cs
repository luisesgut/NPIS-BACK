using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

/// <summary>Catálogos administrables, lectura para cualquier usuario autenticado (docs/api-contract.md §8).</summary>
[ApiController]
[Route("api/catalogs")]
[Authorize]
public sealed class CatalogsController(ICatalogReader catalogReader) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var catalogs = await catalogReader.ListAsync(cancellationToken);
        return Ok(catalogs);
    }
}
