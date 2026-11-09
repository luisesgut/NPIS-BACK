using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Consulta de una cotización de SISPRO por número, para precargar la captura del NPI ("Importar
/// desde SISPRO"). 404 si no existe; 503 si SISPRO no responde o no está configurado. Los precios
/// sólo los ve Comercial (QuotePriceRedactionFilter).
/// </summary>
[ApiController]
[Route("api/quotations")]
[Authorize(Policy = "QuotationsRead")]
public sealed class QuotationsController(IQuotationProvider quotationProvider) : ControllerBase
{
    [HttpGet("{numero}")]
    public async Task<IActionResult> Find(string numero, CancellationToken cancellationToken)
    {
        var quotation = await quotationProvider.FindAsync(numero, cancellationToken);
        return quotation is null ? NotFound() : Ok(quotation);
    }
}
