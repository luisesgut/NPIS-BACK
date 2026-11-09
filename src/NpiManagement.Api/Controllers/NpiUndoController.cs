using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api.Controllers;

/// <summary>Deshacer la última acción (quien la hizo, si nadie actuó después), reabrir un NPI cerrado por error y eliminar un NPI (AdminTotal).</summary>
[ApiController]
[Route("api/npis/{id:guid}")]
[Authorize]
public sealed class NpiUndoController(UndoLastActionCommandHandler undoHandler, ReopenNpiCommandHandler reopenHandler, DeleteNpiCommandHandler deleteHandler, CorrectChangeRequestAreaCommandHandler correctAreaHandler) : ControllerBase
{
    /// <summary>Elimina el NPI para siempre (sólo AdminTotal). ?confirmation= el folio, o BORRADOR si nunca se envió.</summary>
    [HttpDelete]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string? confirmation, CancellationToken cancellationToken)
    {
        await deleteHandler.HandleAsync(new DeleteNpiCommand(id, User.ToCurrentUser(), User.DisplayName(), confirmation), cancellationToken);
        return NoContent();
    }

    [HttpPost("undo-last-action")]
    public Task<NpiDetailDto> UndoLastAction(Guid id, CancellationToken cancellationToken) =>
        undoHandler.HandleAsync(new UndoLastActionCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    /// <summary>Corrige qué área pidió una solicitud de cambios que Comercial aún no atiende (sólo AdminTotal).</summary>
    [HttpPost("change-requests/{requestId:guid}/area")]
    [Authorize(Policy = "AdminTotal")]
    public Task<NpiDetailDto> CorrectChangeRequestArea(Guid id, Guid requestId, CorrectChangeRequestAreaRequest request, CancellationToken cancellationToken) =>
        correctAreaHandler.HandleAsync(new CorrectChangeRequestAreaCommand(id, User.ToCurrentUser(), User.DisplayName(), requestId, request.Area, request.Reason), cancellationToken);

    [HttpPost("reopen")]
    [Authorize(Policy = "AdminTotal")]
    public Task<NpiDetailDto> Reopen(Guid id, ReopenNpiRequest request, CancellationToken cancellationToken) =>
        reopenHandler.HandleAsync(new ReopenNpiCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Reason), cancellationToken);
}
