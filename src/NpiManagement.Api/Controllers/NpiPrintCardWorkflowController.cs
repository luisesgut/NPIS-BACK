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
    LoadPrintCardIntoSystemCommandHandler loadIntoSystemHandler,
    RejectPrintCardCommandHandler rejectPrintCardHandler,
    SetClientChangeItemCommandHandler setClientChangeItemHandler,
    SetPrintCardObservationCommandHandler setPrintCardObservationHandler,
    ReplacePrintCardFileCommandHandler replaceFileHandler) : ControllerBase
{
    public sealed record ReplacePrintCardFileRequest(Guid AttachmentId);

    /// <summary>Reemplaza el archivo del PrintCard vigente mientras Ingeniería no lo ha revisado.</summary>
    [HttpPost("printcard-file")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> ReplacePrintCardFile(Guid id, ReplacePrintCardFileRequest request, CancellationToken cancellationToken) =>
        replaceFileHandler.HandleAsync(new ReplacePrintCardFileCommand(id, User.ToCurrentUser(), User.DisplayName(), request.AttachmentId), cancellationToken);

    [HttpPost("printcards")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> CreateVersion(Guid id, CreatePrintCardVersionRequest request, CancellationToken cancellationToken) =>
        createVersionHandler.HandleAsync(new CreatePrintCardVersionCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Code, request.AttachmentId, request.Note, request.CommercialChecklist), cancellationToken);

    /// <summary>Comercial palomea un punto que Diseño le pidió revisar con el cliente.</summary>
    [HttpPost("printcard-observations/{itemId:guid}")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> SetPrintCardObservation(Guid id, Guid itemId, ClientChangeItemRequest request, CancellationToken cancellationToken) =>
        setPrintCardObservationHandler.HandleAsync(new SetPrintCardObservationCommand(id, User.ToCurrentUser(), User.DisplayName(), itemId, request.Done), cancellationToken);

    [HttpPost("printcard-approvals")]
    public Task<NpiDetailDto> Approve(Guid id, ApprovePrintCardRequest request, CancellationToken cancellationToken) =>
        approveHandler.HandleAsync(new ApprovePrintCardCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Role, request.Note, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("send-printcard-to-client")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> SendToClient(Guid id, CancellationToken cancellationToken) =>
        sendToClientHandler.HandleAsync(new SendPrintCardToClientCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("request-client-changes")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> RequestClientChanges(Guid id, ClientChangesRequest request, CancellationToken cancellationToken) =>
        requestClientChangesHandler.HandleAsync(new RequestClientChangesCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Description, request.Items ?? [], request.AttachmentIds), cancellationToken);

    [HttpPost("reject-client")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> RejectByClient(Guid id, RejectByClientRequest request, CancellationToken cancellationToken) =>
        rejectByClientHandler.HandleAsync(new RejectByClientCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Comment, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("load-printcard")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> LoadIntoSystem(Guid id, CancellationToken cancellationToken) =>
        loadIntoSystemHandler.HandleAsync(new LoadPrintCardIntoSystemCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("reject-printcard")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> RejectPrintCard(Guid id, RejectPrintCardRequest request, CancellationToken cancellationToken) =>
        rejectPrintCardHandler.HandleAsync(new RejectPrintCardCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Reason, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("client-change-items/{itemId:guid}")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> SetClientChangeItem(Guid id, Guid itemId, ClientChangeItemRequest request, CancellationToken cancellationToken) =>
        setClientChangeItemHandler.HandleAsync(new SetClientChangeItemCommand(id, User.ToCurrentUser(), User.DisplayName(), itemId, request.Done), cancellationToken);
}
