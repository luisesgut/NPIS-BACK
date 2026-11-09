using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Acciones de workflow (Fase 5 — ver docs/workflow.md). Cada endpoint corresponde a un único
/// comando de negocio; ninguno acepta un campo "status" genérico. Controller delgado: sólo
/// arma el comando desde los claims del token + el body validado y delega en Application.
/// La autorización dependiente del recurso ocurre dentro de cada handler (NpiAuthorization);
/// aquí sólo [Authorize] exige autenticación — un usuario sin relación con el NPI recibe 404
/// desde el handler, no 403 (docs/authorization-matrix.md §4.6).
/// </summary>
[ApiController]
[Route("api/npis/{id:guid}")]
[Authorize]
public sealed class NpiWorkflowController(
    SubmitNpiCommandHandler submitHandler,
    StartCommercialEditCommandHandler startCommercialEditHandler,
    AssignEngineeringCommandHandler assignEngineeringHandler,
    AssignDesignCommandHandler assignDesignHandler,
    ReassignCommercialCommandHandler reassignCommercialHandler,
    ApproveEngineeringFeasibilityCommandHandler approveEngineeringFeasibilityHandler,
    ReconfirmEngineeringFeasibilityCommandHandler reconfirmEngineeringFeasibilityHandler,
    ApproveDesignFeasibilityCommandHandler approveDesignFeasibilityHandler,
    RequestEngineeringChangesCommandHandler requestEngineeringChangesHandler,
    RejectNoFeasibilityCommandHandler rejectNoFeasibilityHandler,
    ValidateArtworkCommandHandler validateArtworkHandler,
    RegisterSapPtCommandHandler registerSapPtHandler,
    RegisterSapOrderCommandHandler registerSapOrderHandler,
    RegisterEtCommandHandler registerEtHandler,
    LoadEtIntoSisproCommandHandler loadEtIntoSisproHandler,
    RejectSuppliesTemporaryCommandHandler rejectSuppliesTemporaryHandler,
    ResolveSuppliesAgreementCommandHandler resolveSuppliesAgreementHandler,
    RejectSuppliesTotalCommandHandler rejectSuppliesTotalHandler,
    RequestInformationCommandHandler requestInformationHandler,
    ResolveClientInformationCommandHandler resolveClientInformationHandler,
    RespondCommercialInformationCommandHandler respondCommercialInformationHandler,
    RespondEngineeringInformationCommandHandler respondEngineeringInformationHandler,
    CancelNpiCommandHandler cancelHandler) : ControllerBase
{
    [HttpPost("submit")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> Submit(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] SubmitNpiRequest? request, CancellationToken cancellationToken) =>
        submitHandler.HandleAsync(new SubmitNpiCommand(id, User.ToCurrentUser(), User.DisplayName(), request?.Comment), cancellationToken);

    /// <summary>Comercial empieza a modificar el NPI por su cuenta (hasta antes del PT); después edita y reenvía con "submit".</summary>
    [HttpPost("commercial-edit")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> StartCommercialEdit(Guid id, StartCommercialEditRequest request, CancellationToken cancellationToken) =>
        startCommercialEditHandler.HandleAsync(new StartCommercialEditCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Reason, request.RedoPrintCard), cancellationToken);

    [HttpPost("assign-engineering")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> AssignEngineering(Guid id, AssignUserRequest request, CancellationToken cancellationToken) =>
        assignEngineeringHandler.HandleAsync(new AssignEngineeringCommand(id, User.ToCurrentUser(), User.DisplayName(), request.UserId), cancellationToken);

    [HttpPost("assign-design")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> AssignDesign(Guid id, AssignDesignRequest request, CancellationToken cancellationToken) =>
        assignDesignHandler.HandleAsync(new AssignDesignCommand(id, User.ToCurrentUser(), User.DisplayName(), request.UserId, request.ArtClassification, request.HasLabValues), cancellationToken);

    [HttpPost("reassign-commercial")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> ReassignCommercial(Guid id, AssignUserRequest request, CancellationToken cancellationToken) =>
        reassignCommercialHandler.HandleAsync(new ReassignCommercialCommand(id, User.ToCurrentUser(), User.DisplayName(), request.UserId), cancellationToken);

    [HttpPost("engineering-feasibility")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> EngineeringFeasibility(Guid id, EngineeringFeasibilityRequest request, CancellationToken cancellationToken) =>
        approveEngineeringFeasibilityHandler.HandleAsync(new ApproveEngineeringFeasibilityCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note, request.ConfirmedFieldKeys), cancellationToken);

    /// <summary>Ingeniería confirma que sigue siendo factible después de que Comercial cambió campos que ya había revisado.</summary>
    [HttpPost("engineering-reconfirmation")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> EngineeringReconfirmation(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] NoteRequest? request, CancellationToken cancellationToken) =>
        reconfirmEngineeringFeasibilityHandler.HandleAsync(new ReconfirmEngineeringFeasibilityCommand(id, User.ToCurrentUser(), User.DisplayName(), request?.Note), cancellationToken);

    [HttpPost("design-feasibility")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> DesignFeasibility(Guid id, DesignFeasibilityRequest request, CancellationToken cancellationToken) =>
        approveDesignFeasibilityHandler.HandleAsync(new ApproveDesignFeasibilityCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note, request.ArtClassification, request.HasLabValues), cancellationToken);

    [HttpPost("request-changes")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> RequestChanges(Guid id, RequestChangesRequest request, CancellationToken cancellationToken) =>
        requestEngineeringChangesHandler.HandleAsync(new RequestEngineeringChangesCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Description, request.FieldKeys ?? [], request.FieldNotes, request.RedoPrintCard, request.ImageAttachmentIds), cancellationToken);

    [HttpPost("reject-no-feasibility")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> RejectNoFeasibility(Guid id, NoteRequest request, CancellationToken cancellationToken) =>
        rejectNoFeasibilityHandler.HandleAsync(new RejectNoFeasibilityCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note), cancellationToken);

    [HttpPost("validate-artwork")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> ValidateArtwork(Guid id, CancellationToken cancellationToken) =>
        validateArtworkHandler.HandleAsync(new ValidateArtworkCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("register-sap-pt")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> RegisterSapPt(Guid id, RegisterSapPtRequest request, CancellationToken cancellationToken) =>
        registerSapPtHandler.HandleAsync(new RegisterSapPtCommand(id, User.ToCurrentUser(), User.DisplayName(), request.CodigoPT, request.PtName, request.SapTable), cancellationToken);

    [HttpPost("register-sap-order")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> RegisterSapOrder(Guid id, RegisterSapOrderRequest request, CancellationToken cancellationToken) =>
        registerSapOrderHandler.HandleAsync(new RegisterSapOrderCommand(id, User.ToCurrentUser(), User.DisplayName(), request.PedidoSapNumber), cancellationToken);

    [HttpPost("register-et")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> RegisterEt(Guid id, CancellationToken cancellationToken) =>
        registerEtHandler.HandleAsync(new RegisterEtCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("load-et-sispro")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> LoadEtIntoSispro(Guid id, CancellationToken cancellationToken) =>
        loadEtIntoSisproHandler.HandleAsync(new LoadEtIntoSisproCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("reject-supplies-temporary")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> RejectSuppliesTemporary(Guid id, NoteRequest request, CancellationToken cancellationToken) =>
        rejectSuppliesTemporaryHandler.HandleAsync(new RejectSuppliesTemporaryCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Note), cancellationToken);

    [HttpPost("resolve-supplies-agreement")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> ResolveSuppliesAgreement(Guid id, ResolveSuppliesAgreementRequest request, CancellationToken cancellationToken) =>
        resolveSuppliesAgreementHandler.HandleAsync(new ResolveSuppliesAgreementCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Text, request.EvidenceAttachmentId), cancellationToken);

    [HttpPost("reject-supplies-total")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> RejectSuppliesTotal(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] NoteRequest? request, CancellationToken cancellationToken) =>
        rejectSuppliesTotalHandler.HandleAsync(new RejectSuppliesTotalCommand(id, User.ToCurrentUser(), User.DisplayName(), request?.Note), cancellationToken);

    [HttpPost("request-information")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> RequestInformation(Guid id, RequestInformationRequest request, CancellationToken cancellationToken) =>
        requestInformationHandler.HandleAsync(new RequestInformationCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Type, request.Text, request.ImageAttachmentIds), cancellationToken);

    [HttpPost("resolve-client-information")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> ResolveClientInformation(Guid id, CancellationToken cancellationToken) =>
        resolveClientInformationHandler.HandleAsync(new ResolveClientInformationCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("respond-commercial-information")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> RespondCommercialInformation(Guid id, RespondCommercialInformationRequest request, CancellationToken cancellationToken) =>
        respondCommercialInformationHandler.HandleAsync(new RespondCommercialInformationCommand(id, User.ToCurrentUser(), User.DisplayName(), request.ResponseText), cancellationToken);

    [HttpPost("respond-engineering-information")]
    [Authorize(Policy = "AreaIngenieria")]
    public Task<NpiDetailDto> RespondEngineeringInformation(Guid id, RespondEngineeringInformationRequest request, CancellationToken cancellationToken) =>
        respondEngineeringInformationHandler.HandleAsync(new RespondEngineeringInformationCommand(id, User.ToCurrentUser(), User.DisplayName(), request.ResponseText), cancellationToken);

    [HttpPost("cancel")]
    [Authorize(Policy = "AreaComercial")]
    public Task<NpiDetailDto> Cancel(Guid id, CancelRequest request, CancellationToken cancellationToken) =>
        cancelHandler.HandleAsync(new CancelNpiCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Reason), cancellationToken);
}
