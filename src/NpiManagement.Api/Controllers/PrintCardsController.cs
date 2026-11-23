using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.PrintCards;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Módulo PrintCards (2026-11-23, v1.2.0): resumen del área, indicadores por diseñador e historial.
/// Diseño (admin y diseñadores), AdminTotal y Dirección; el diseñador sólo ve lo suyo (lo decide el handler).
/// months: 1, 3, 6, 12 o 0 (todo). designerUserId sólo cuenta para admins y Dirección.
/// </summary>
[ApiController]
[Route("api/printcards")]
[Authorize(Policy = "PrintCardsReport")]
public sealed class PrintCardsController(PrintCardReportQueryHandler handler, IPrintCardReportExporter exporter) : ControllerBase
{
    [HttpGet("report")]
    public async Task<ActionResult<PrintCardReportDto>> Report([FromQuery] int months = 6, [FromQuery] string? cliente = null, [FromQuery] Guid? designerUserId = null, CancellationToken cancellationToken = default) =>
        Ok(await handler.HandleAsync(new PrintCardReportQuery(User.ToCurrentUser(), months, cliente, designerUserId), cancellationToken));

    [HttpGet("report/export")]
    public async Task<IActionResult> Export([FromQuery] int months = 6, [FromQuery] string? cliente = null, [FromQuery] Guid? designerUserId = null, CancellationToken cancellationToken = default)
    {
        var records = await handler.ExportRecordsAsync(new PrintCardReportQuery(User.ToCurrentUser(), months, cliente, designerUserId), cancellationToken);
        var file = exporter.Build(records);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
