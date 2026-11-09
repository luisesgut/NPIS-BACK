using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Etapa "Cotización SISPRO" del NPI: ligar la cotización y registrar la respuesta del cliente al
/// precio. Todas son de Comercial; igual que el resto del workflow, la autorización sobre el NPI
/// concreto la hace cada handler (404 si no le corresponde).
/// </summary>
[ApiController]
[Route("api/npis/{id:guid}/sispro-quote")]
[Authorize(Policy = "AreaComercial")]
public sealed class NpiSisproQuoteController(
    LinkSisproQuoteCommandHandler linkHandler,
    AcceptSisproQuoteCommandHandler acceptHandler,
    RequestSisproQuoteAdjustmentCommandHandler adjustmentHandler,
    RejectByPriceCommandHandler rejectHandler) : ControllerBase
{
    [HttpPost("link")]
    public Task<NpiDetailDto> Link(Guid id, LinkSisproQuoteRequest request, CancellationToken cancellationToken) =>
        linkHandler.HandleAsync(new LinkSisproQuoteCommand(id, User.ToCurrentUser(), User.DisplayName(), request.NumeroCotizacion), cancellationToken);

    [HttpPost("accept")]
    public Task<NpiDetailDto> Accept(Guid id, AcceptSisproQuoteRequest request, CancellationToken cancellationToken) =>
        acceptHandler.HandleAsync(new AcceptSisproQuoteCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("adjustment")]
    public Task<NpiDetailDto> RequestAdjustment(Guid id, RequiredNoteRequest request, CancellationToken cancellationToken) =>
        adjustmentHandler.HandleAsync(new RequestSisproQuoteAdjustmentCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note), cancellationToken);

    [HttpPost("reject")]
    public Task<NpiDetailDto> RejectByPrice(Guid id, RejectByPriceRequest request, CancellationToken cancellationToken) =>
        rejectHandler.HandleAsync(new RejectByPriceCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Comment, request.EvidenceAttachmentId), cancellationToken);
}
