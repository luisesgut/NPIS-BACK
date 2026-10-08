using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

public sealed record CatalogOptionValueRequest([Required] string Value);

public sealed record CatalogOptionActiveRequest(bool IsActive);

public sealed record CatalogReorderRequest([Required] IReadOnlyList<Guid> OptionIds);

/// <summary>
/// Catálogos: lectura para cualquier usuario autenticado (docs/api-contract.md §8) y administración
/// de opciones para Ingeniería (AdminTotal, AdminIngenieria y UsuarioIngenieria).
/// </summary>
[ApiController]
[Route("api/catalogs")]
[Authorize]
public sealed class CatalogsController(ICatalogReader catalogReader, ICatalogAdminService catalogAdmin) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var catalogs = await catalogReader.ListAsync(cancellationToken);
        return Ok(catalogs);
    }

    /// <summary>Catálogos con ids, opciones protegidas y cuántos NPIs usan cada opción.</summary>
    [HttpGet("admin")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<ActionResult<IReadOnlyList<CatalogAdminDto>>> AdminList(CancellationToken cancellationToken) =>
        Ok(await catalogAdmin.ListAsync(cancellationToken));

    [HttpPost("{catalogId:guid}/options")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<ActionResult<CatalogAdminOptionDto>> AddOption(Guid catalogId, CatalogOptionValueRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await catalogAdmin.AddOptionAsync(catalogId, request.Value, User.ToCurrentUser(), User.DisplayName(), cancellationToken));

    [HttpPut("options/{optionId:guid}")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<IActionResult> RenameOption(Guid optionId, CatalogOptionValueRequest request, CancellationToken cancellationToken)
    {
        await catalogAdmin.RenameOptionAsync(optionId, request.Value, User.ToCurrentUser(), User.DisplayName(), cancellationToken);
        return NoContent();
    }

    [HttpPost("options/{optionId:guid}/active")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<IActionResult> SetActive(Guid optionId, CatalogOptionActiveRequest request, CancellationToken cancellationToken)
    {
        await catalogAdmin.SetActiveAsync(optionId, request.IsActive, User.ToCurrentUser(), User.DisplayName(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{catalogId:guid}/order")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<IActionResult> Reorder(Guid catalogId, CatalogReorderRequest request, CancellationToken cancellationToken)
    {
        await catalogAdmin.ReorderAsync(catalogId, request.OptionIds, User.ToCurrentUser(), User.DisplayName(), cancellationToken);
        return NoContent();
    }

    [HttpGet("changes")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<ActionResult<IReadOnlyList<CatalogChangeDto>>> Changes([FromQuery] string? catalogKey, CancellationToken cancellationToken) =>
        Ok(await catalogAdmin.ListChangesAsync(catalogKey, cancellationToken));
}
