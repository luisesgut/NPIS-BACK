using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

/// <summary>Búsqueda de cotizaciones por número, para precargar los campos "verdes" del NPI (docs/api-contract.md §8).</summary>
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
