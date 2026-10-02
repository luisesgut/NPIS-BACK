using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api.Controllers;

/// <summary>Acciones de PrintCard (Fase 5, grupo 4 de docs/workflow.md) — mismo patrón que NpiWorkflowController.</summary>
[ApiController]
[Route("api/npis/{id:guid}")]
[Authorize]
public sealed class NpiPrintCardWorkflowController(
    CreatePrintCardVersionCommandHandler createVersionHandler,
    ApprovePrintCardCommandHandler approveHandler,
    SendPrintCardToClientCommandHandler sendToClientHandler,
    RequestClientChangesCommandHandler requestClientChangesHandler,
    RejectByClientCommandHandler rejectByClientHandler,
    LoadPrintCardIntoSystemCommandHandler loadIntoSystemHandler) : ControllerBase
{
    [HttpPost("printcards")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> CreateVersion(Guid id, CreatePrintCardVersionRequest request, CancellationToken cancellationToken) =>
        createVersionHandler.HandleAsync(new CreatePrintCardVersionCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Code, request.AttachmentId, request.Note), cancellationToken);

    [HttpPost("printcard-approvals")]
    public Task<NpiDetailDto> Approve(Guid id, ApprovePrintCardRequest request, CancellationToken cancellationToken) =>
        approveHandler.HandleAsync(new ApprovePrintCardCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Role, request.Note, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("send-printcard-to-client")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> SendToClient(Guid id, CancellationToken cancellationToken) =>
        sendToClientHandler.HandleAsync(new SendPrintCardToClientCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("request-client-changes")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> RequestClientChanges(Guid id, DescriptionRequest request, CancellationToken cancellationToken) =>
        requestClientChangesHandler.HandleAsync(new RequestClientChangesCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Description), cancellationToken);

    [HttpPost("reject-client")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> RejectByClient(Guid id, RejectByClientRequest request, CancellationToken cancellationToken) =>
        rejectByClientHandler.HandleAsync(new RejectByClientCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Comment, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("load-printcard")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> LoadIntoSystem(Guid id, CancellationToken cancellationToken) =>
        loadIntoSystemHandler.HandleAsync(new LoadPrintCardIntoSystemCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);
}
