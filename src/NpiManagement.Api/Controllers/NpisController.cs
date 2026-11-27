using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// CRUD controlado de borradores NPI (Fase 4 — ver docs/api-contract.md §3). No admite ningún
/// campo de workflow/estado: eso llega en Fase 5 como acciones dedicadas (submit, assign-*, etc.).
/// Controller delgado: valida el DTO de entrada y delega en el caso de uso de Application.
/// </summary>
[ApiController]
[Route("api/npis")]
[Authorize]
public sealed class NpisController(
    CreateNpiDraftCommandHandler createHandler,
    UpdateNpiDraftCommandHandler updateHandler,
    GetNpiByIdQueryHandler getByIdHandler,
    ListNpisQueryHandler listHandler,
    GetNpiAuditQueryHandler auditHandler,
    GetNpiTimerEventsQueryHandler timerEventsHandler,
    GetNpiAssigneesQueryHandler assigneesHandler,
    GetCaptureHistoryQueryHandler captureHistoryHandler,
    GetTeamCandidatesQueryHandler teamCandidatesHandler,
    GetNpiBasedOnQueryHandler basedOnHandler,
    GetSapOrdersQueryHandler sapOrdersHandler,
    NpiManagement.Application.Npis.SapOrders.GetPendingSapOrdersQueryHandler pendingSapOrdersHandler,
    GetSapProductsQueryHandler sapProductsHandler,
    NpiManagement.Application.Npis.Advance.GetAdvanceStatusQueryHandler advanceStatusHandler,
    NpiManagement.Application.Npis.Advance.SyncNpiWithAdvanceCommandHandler advanceSyncHandler,
    IValidator<CreateNpiDraftCommand> createValidator,
    IValidator<UpdateNpiDraftCommand> updateValidator) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "AreaComercial")]
    public async Task<ActionResult<Guid>> Create(CreateNpiDraftRequest request, CancellationToken cancellationToken)
    {
        var user = User.ToCurrentUser();
        var command = new CreateNpiDraftCommand(user.UserId, user.Role, User.DisplayName(), request.Capture, request.Priority, request.PreAssignedEngineeringUserId, request.BasedOnNpiId, user);

        var validation = await createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid) return this.ValidationProblem(validation);

        var id = await createHandler.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "AreaComercial")]
    public async Task<ActionResult<NpiDetailDto>> Update(Guid id, UpdateNpiDraftRequest request, CancellationToken cancellationToken)
    {
        var user = User.ToCurrentUser();
        var command = new UpdateNpiDraftCommand(id, user, request.Capture, request.Priority, request.ExpectedRowVersion, User.DisplayName());

        var validation = await updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid) return this.ValidationProblem(validation);

        var updated = await updateHandler.HandleAsync(command, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// NPIs con PT y sin orden de venta de quien consulta (y de su equipo, si es director), y si eso ya le impide
    /// crear NPIs nuevos (2026-11-27). Para las demás áreas regresa la lista vacía.
    /// </summary>
    [HttpGet("pending-sap-orders")]
    public Task<NpiManagement.Application.Npis.SapOrders.PendingSapOrdersDto> PendingSapOrders(CancellationToken cancellationToken) =>
        pendingSapOrdersHandler.HandleAsync(new NpiManagement.Application.Npis.SapOrders.GetPendingSapOrdersQuery(User.ToCurrentUser()), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NpiDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await getByIdHandler.HandleAsync(new GetNpiByIdQuery(id, User.ToCurrentUser()), cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NpiListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] string? lifecycleStatus,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] Guid? createdByUserId,
        [FromQuery] string? priority,
        [FromQuery] string? cliente,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pendingClosureArea = null,
        [FromQuery] string? situation = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? area = null,
        [FromQuery] bool mine = false,
        [FromQuery] bool team = false,
        [FromQuery] bool copySources = false,
        CancellationToken cancellationToken = default)
    {
        var filter = new NpiListFilter(search, lifecycleStatus, assignedToUserId, createdByUserId, priority, cliente, page, pageSize, pendingClosureArea, situation, stage, area);
        var result = await listHandler.HandleAsync(new ListNpisQuery(filter, User.ToCurrentUser(), mine, team, copySources), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEventDto>>> GetAudit(Guid id, CancellationToken cancellationToken)
    {
        var result = await auditHandler.HandleAsync(new GetNpiAuditQuery(id, User.ToCurrentUser()), cancellationToken);
        return Ok(result);
    }

    /// <summary>Captura inicial (al primer envío) y bitácora de cambios campo por campo: quién lo pidió y quién lo modificó.</summary>
    [HttpGet("{id:guid}/capture-history")]
    public async Task<ActionResult<CaptureHistoryDto>> GetCaptureHistory(Guid id, CancellationToken cancellationToken) =>
        Ok(await captureHistoryHandler.HandleAsync(new GetCaptureHistoryQuery(id, User.ToCurrentUser()), cancellationToken));

    [HttpGet("{id:guid}/timer-events")]
    public async Task<ActionResult<IReadOnlyList<TimerEventDto>>> GetTimerEvents(Guid id, CancellationToken cancellationToken)
    {
        var result = await timerEventsHandler.HandleAsync(new GetNpiTimerEventsQuery(id, User.ToCurrentUser()), cancellationToken);
        return Ok(result);
    }

    /// <summary>Nombres de los responsables — para cualquier rol con acceso al NPI, no sólo admins.</summary>
    [HttpGet("{id:guid}/assignees")]
    public async Task<ActionResult<NpiAssigneesDto>> GetAssignees(Guid id, CancellationToken cancellationToken)
    {
        var result = await assigneesHandler.HandleAsync(new GetNpiAssigneesQuery(id, User.ToCurrentUser()), cancellationToken);
        return Ok(result);
    }

    /// <summary>Órdenes de venta de SAP con el PT del NPI (la primera es la que corresponde).</summary>
    [HttpGet("{id:guid}/sap-orders")]
    public async Task<ActionResult<NpiManagement.Application.Abstractions.SapOrdersLookupDto>> GetSapOrders(Guid id, CancellationToken cancellationToken) =>
        Ok(await sapOrdersHandler.HandleAsync(new GetSapOrdersQuery(id, User.ToCurrentUser()), cancellationToken));

    /// <summary>Estado del PrintCard en Advance (documentos subidos/pendientes); sólo lee.</summary>
    [HttpGet("{id:guid}/advance")]
    public async Task<ActionResult<NpiManagement.Application.Npis.Advance.AdvanceSyncResult>> GetAdvance(Guid id, CancellationToken cancellationToken) =>
        Ok(await advanceStatusHandler.HandleAsync(new NpiManagement.Application.Npis.Advance.GetAdvanceStatusQuery(id, User.ToCurrentUser()), cancellationToken));

    /// <summary>"Revisar Advance": aplica de inmediato lo que diga Advance (carga y PrintCard final; sólo NPIs nuevos).</summary>
    [HttpPost("{id:guid}/advance/sync")]
    public async Task<ActionResult<NpiManagement.Application.Npis.Advance.AdvanceSyncResult>> SyncAdvance(Guid id, CancellationToken cancellationToken) =>
        Ok(await advanceSyncHandler.HandleAsync(new NpiManagement.Application.Npis.Advance.SyncNpiWithAdvanceCommand(id, User.ToCurrentUser()), cancellationToken));

    /// <summary>PT en SAP por el código del PrintCard final del NPI.</summary>
    [HttpGet("{id:guid}/sap-products")]
    public async Task<ActionResult<NpiManagement.Application.Abstractions.SapProductsLookupDto>> GetSapProducts(Guid id, CancellationToken cancellationToken) =>
        Ok(await sapProductsHandler.HandleAsync(new GetSapProductsQuery(id, User.ToCurrentUser()), cancellationToken));

    /// <summary>De qué NPI partió y qué campos cambian (204 si no partió de ninguno).</summary>
    [HttpGet("{id:guid}/based-on")]
    public async Task<ActionResult<NpiBasedOnDto>> GetBasedOn(Guid id, CancellationToken cancellationToken) =>
        await basedOnHandler.HandleAsync(new GetNpiBasedOnQuery(id, User.ToCurrentUser()), cancellationToken) is { } result ? Ok(result) : NoContent();

    /// <summary>Equipo por NPI: compañeros del área que el responsable (o su admin) puede sumar.</summary>
    [HttpGet("{id:guid}/team-candidates")]
    public async Task<ActionResult<IReadOnlyList<NpiAssigneeDto>>> GetTeamCandidates(Guid id, [FromQuery] AreaType area, CancellationToken cancellationToken) =>
        Ok(await teamCandidatesHandler.HandleAsync(new GetTeamCandidatesQuery(id, User.ToCurrentUser(), area), cancellationToken));
}
