using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Rechazos de Diseño (con cambios, no factible, artes) y la respuesta de Comercial a las artes
/// rechazadas. Mismas reglas que NpiWorkflowController: la autorización por recurso la hace cada
/// handler y un usuario sin relación con el NPI recibe 404.
/// </summary>
[ApiController]
[Route("api/npis/{id:guid}")]
[Authorize]
public sealed class NpiDesignReviewController(
    RequestDesignChangesCommandHandler requestDesignChangesHandler,
    RejectDesignNoFeasibilityCommandHandler rejectDesignNoFeasibilityHandler,
    RejectArtworkCommandHandler rejectArtworkHandler,
    ResolveArtworkRejectionCommandHandler resolveArtworkRejectionHandler) : ControllerBase
{
    [HttpPost("request-design-changes")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> RequestDesignChanges(Guid id, RequestChangesRequest request, CancellationToken cancellationToken) =>
        requestDesignChangesHandler.HandleAsync(new RequestDesignChangesCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Description, request.FieldKeys ?? []), cancellationToken);

    [HttpPost("reject-design-no-feasibility")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> RejectDesignNoFeasibility(Guid id, RequiredNoteRequest request, CancellationToken cancellationToken) =>
        rejectDesignNoFeasibilityHandler.HandleAsync(new RejectDesignNoFeasibilityCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note), cancellationToken);

    [HttpPost("reject-artwork")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> RejectArtwork(Guid id, RejectArtworkRequest request, CancellationToken cancellationToken) =>
        rejectArtworkHandler.HandleAsync(new RejectArtworkCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Reason, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("resolve-artwork-rejection")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> ResolveArtworkRejection(Guid id, NoteRequest request, CancellationToken cancellationToken) =>
        resolveArtworkRejectionHandler.HandleAsync(new ResolveArtworkRejectionCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note), cancellationToken);
}
