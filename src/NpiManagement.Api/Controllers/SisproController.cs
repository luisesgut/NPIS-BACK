using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

public sealed record UpsertSisproEquivalenceRequest([Required] string MaterialSispro, [Required] string TipoPelicula, string? Terminado, string? FormulaLdpe);

/// <summary>Interruptores de SISPRO que el front necesita para pintar la ruta igual que el back.</summary>
public sealed record SisproConfigDto(bool CotizacionObligatoria);

/// <summary>
/// Administración de la integración con SISPRO. Las equivalencias de materiales las mantiene
/// Ingeniería (igual que los catálogos); la prueba de conexión la usa TI después de capturar las
/// credenciales en el servidor. Las credenciales nunca pasan por la API.
/// </summary>
[ApiController]
[Route("api/sispro")]
[Authorize]
public sealed class SisproController(ISisproAdminService sispro) : ControllerBase
{
    [HttpGet("equivalences")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<ActionResult<IReadOnlyList<SisproEquivalenceDto>>> ListEquivalences(CancellationToken cancellationToken) =>
        Ok(await sispro.ListEquivalencesAsync(cancellationToken));

    [HttpPut("equivalences")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<ActionResult<SisproEquivalenceDto>> UpsertEquivalence(UpsertSisproEquivalenceRequest request, CancellationToken cancellationToken) =>
        Ok(await sispro.UpsertEquivalenceAsync(request.MaterialSispro, request.TipoPelicula, request.Terminado, request.FormulaLdpe, User.DisplayName(), cancellationToken));

    [HttpDelete("equivalences/{equivalenceId:guid}")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<IActionResult> DeleteEquivalence(Guid equivalenceId, CancellationToken cancellationToken)
    {
        await sispro.DeleteEquivalenceAsync(equivalenceId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Sispro:CotizacionObligatoria (SisproQuoteGate). Apagado, el front quita de la ruta las etapas
    /// "Cotización SISPRO" y "Precio aceptado" y no detiene el PrintCard esperando el precio.
    /// </summary>
    [HttpGet("config")]
    public ActionResult<SisproConfigDto> Config() => Ok(new SisproConfigDto(Domain.SisproQuoteGate.Enabled));

    /// <summary>Prueba de conexión: inicia sesión en SISPRO con el usuario configurado.</summary>
    [HttpGet("status")]
    [Authorize(Policy = "AnyAdmin")]
    public async Task<ActionResult<SisproStatusDto>> Status(CancellationToken cancellationToken) =>
        Ok(await sispro.CheckStatusAsync(cancellationToken));
}
