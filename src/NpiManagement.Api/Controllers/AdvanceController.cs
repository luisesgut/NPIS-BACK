using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

/// <summary>Si la conexión con Advance está encendida y desde cuándo aplica (NPIs creados desde esa fecha).</summary>
public sealed record AdvanceConfigDto(bool Enabled, DateTimeOffset? AppliesFrom);

/// <summary>
/// Configuración de Advance para el front (2026-11-12): el aviso a Diseño sólo sale cuando la
/// conexión ya está encendida en el servidor, para no anunciar algo que todavía no funciona.
/// </summary>
[ApiController]
[Route("api/advance")]
[Authorize]
public sealed class AdvanceController(IAdvanceClient advance) : ControllerBase
{
    [HttpGet("config")]
    public ActionResult<AdvanceConfigDto> GetConfig() => Ok(new AdvanceConfigDto(advance.IsEnabled, advance.AppliesFrom));
}
