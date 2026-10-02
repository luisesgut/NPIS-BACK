using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Adjuntos del NPI — Fase 6 (docs/api-contract.md §7). Se suben ANTES de la acción de workflow
/// que los referencia (p. ej. subir evidencia y luego llamar printcard-approvals con el
/// AttachmentId resultante) — nunca embebidos en el mismo payload JSON de la acción.
/// </summary>
[ApiController]
[Route("api/npis/{id:guid}/attachments")]
[Authorize]
public sealed class NpiAttachmentsController(
    RegisterAttachmentCommandHandler registerHandler,
    GetAttachmentForDownloadQueryHandler downloadHandler) : ControllerBase
{
    /// <summary>Límite razonable de tamaño de request para evitar que un cliente intente enviar
    /// un archivo enorme antes de que la validación de negocio (IAttachmentPolicy) lo rechace.</summary>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid id, IFormFile file, [FromForm] AttachmentContext context, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var command = new RegisterAttachmentCommand(id, User.ToCurrentUser(), User.DisplayName(), file.FileName, file.ContentType, file.Length, stream, context);
        var attachment = await registerHandler.HandleAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, attachment);
    }

    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var download = await downloadHandler.HandleAsync(new GetAttachmentForDownloadQuery(id, attachmentId, User.ToCurrentUser()), cancellationToken);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
