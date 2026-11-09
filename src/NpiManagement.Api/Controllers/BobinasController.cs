using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Bobinas;

namespace NpiManagement.Api.Controllers;

public sealed record MaterialExactoRequest(Guid? MaterialId);

public sealed record CoreBobinaRequest([Range(0.0001, 1)] double DiametroExteriorM, [Range(0, 1)] double KgPorCm, bool IsActive);

/// <summary>
/// Cálculo peso ↔ diámetro de bobina (especificación "Cálculos de bobina", ex Excel convertidor).
/// Todo es exclusivo de Ingeniería (AdminTotal, AdminIngenieria, UsuarioIngenieria): calcular, leer el
/// catálogo de densidades, administrarlo y elegir el material exacto de un NPI.
/// </summary>
[ApiController]
[Route("api/bobinas")]
[Authorize(Policy = "AreaIngenieria")]
public sealed class BobinasController(BobinaCalculatorService calculator, IBobinaCatalogStore store) : ControllerBase
{
    [HttpGet("catalogo")]
    public async Task<ActionResult<CatalogoBobinaDto>> Catalogo(CancellationToken cancellationToken) =>
        Ok(await store.GetAsync(cancellationToken));

    /// <summary>Calcula sobre la captura del formulario (guardada o no). Con npiId usa los materiales exactos de Ingeniería.</summary>
    [HttpPost("calcular-captura")]
    public async Task<ActionResult<CalculoBobinaDto>> CalcularCaptura(CalculoCapturaRequest request, CancellationToken cancellationToken) =>
        Ok(await calculator.CalcularCapturaAsync(request, cancellationToken));

    [HttpPost("calcular")]
    public async Task<ActionResult<CalculoBobinaDto>> CalcularLibre(CalculoLibreRequest request, CancellationToken cancellationToken) =>
        Ok(await calculator.CalcularLibreAsync(request, cancellationToken));

    [HttpGet("npis/{npiId:guid}/materiales-exactos")]
    public async Task<ActionResult<IReadOnlyList<MaterialExactoDto>>> MaterialesExactos(Guid npiId, CancellationToken cancellationToken) =>
        Ok(await store.GetMaterialesExactosAsync(npiId, cancellationToken));

    [HttpPut("npis/{npiId:guid}/materiales-exactos/{capa}")]
    public async Task<IActionResult> SetMaterialExacto(Guid npiId, string capa, MaterialExactoRequest request, CancellationToken cancellationToken)
    {
        await store.SetMaterialExactoAsync(npiId, capa, request.MaterialId, User.ToCurrentUser(), User.DisplayName(), cancellationToken);
        return NoContent();
    }

    [HttpPost("materiales")]
    public async Task<ActionResult<MaterialBobinaDto>> CrearMaterial(MaterialBobinaInput input, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await store.SaveMaterialAsync(null, input, User.ToCurrentUser(), User.DisplayName(), cancellationToken));

    [HttpPut("materiales/{id:guid}")]
    public async Task<ActionResult<MaterialBobinaDto>> GuardarMaterial(Guid id, MaterialBobinaInput input, CancellationToken cancellationToken) =>
        Ok(await store.SaveMaterialAsync(id, input, User.ToCurrentUser(), User.DisplayName(), cancellationToken));

    [HttpPut("cores/{id:guid}")]
    public async Task<IActionResult> GuardarCore(Guid id, CoreBobinaRequest request, CancellationToken cancellationToken)
    {
        await store.SaveCoreAsync(id, request.DiametroExteriorM, request.KgPorCm, request.IsActive, cancellationToken);
        return NoContent();
    }

    [HttpPut("parametros")]
    public async Task<IActionResult> GuardarParametros(ParametrosBobinaDto request, CancellationToken cancellationToken)
    {
        await store.SaveParametrosAsync(request, cancellationToken);
        return NoContent();
    }
}
