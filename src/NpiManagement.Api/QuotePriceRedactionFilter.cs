using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api;

/// <summary>
/// Quita los precios de cotización de toda respuesta que los lleve (detalle del NPI o consulta de
/// cotización) cuando quien pregunta no es de Comercial. Filtro global: así ningún endpoint nuevo
/// que devuelva un NPI se olvida de hacerlo.
/// </summary>
public sealed class QuotePriceRedactionFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null } result
            && context.HttpContext.User.Identity?.IsAuthenticated == true
            && !QuotePriceRedaction.CanSeePrices(context.HttpContext.User.ToCurrentUser().Role))
        {
            result.Value = result.Value switch
            {
                NpiDetailDto detail => QuotePriceRedaction.Redact(detail),
                QuotationSnapshot snapshot => QuotePriceRedaction.Redact(snapshot),
                _ => result.Value,
            };
        }
        return next();
    }
}
