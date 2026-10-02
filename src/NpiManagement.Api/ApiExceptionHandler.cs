using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain.Exceptions;

namespace NpiManagement.Api;

/// <summary>
/// Traduce excepciones de Application/Domain a ProblemDetails. NotFoundException cubre tanto "no
/// existe" como "fuera del alcance de autorización" (mismo 404 a propósito — ver
/// docs/authorization-matrix.md §4.6). RequestValidationException cubre validaciones que no
/// pasan por FluentValidation (p. ej. límites de adjuntos) — 400. InvalidNpiTransitionException
/// (acción pedida en un estado/precondición que no la permite, ver workflow.md) es un conflicto
/// de estado del recurso — 409. NpiValidationException (falta un dato obligatorio del propio
/// invariante del agregado, p. ej. evidencia de aprobación del cliente) es un 400: el cliente
/// puede corregir la solicitud y reintentar. Antes de Fase 8 ninguna de las dos tenía mapeo —
/// cualquier regla de negocio del dominio caía como 500 genérico en vez de un error claro
/// (reproducido en vivo contra SQL Server real). DbUpdateConcurrencyException (Fase 8, ADR-004 —
/// el rowversion es invisible para Domain/Application, pero un handler que carga y guarda el
/// mismo Npi dentro de una sola request puede perder una carrera contra otra request concurrente)
/// se traduce a 409: el cliente debe volver a consultar el detalle, nunca reintentar ciegamente.
/// Cualquier otra excepción cae al manejador por defecto de ASP.NET Core (500, sin detalle en
/// producción).
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Recurso no encontrado.",
                Detail = exception.Message,
                Type = "not-found",
            },
            RequestValidationException rv => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitud inválida.",
                Detail = rv.Message,
                Type = rv.Code,
            },
            InvalidNpiTransitionException tx => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "La acción no es válida en el estado actual del NPI.",
                Detail = tx.Message,
                Type = tx.Code,
            },
            NpiValidationException nv => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitud inválida.",
                Detail = nv.Message,
                Type = nv.Code,
            },
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "El NPI fue modificado por otro usuario mientras tanto.",
                Detail = "Vuelve a consultar el detalle del NPI antes de reintentar la acción.",
                Type = "concurrency-conflict",
            },
            _ => null,
        };

        if (problem is null) return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
