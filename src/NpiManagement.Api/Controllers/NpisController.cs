using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;

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
    IValidator<CreateNpiDraftCommand> createValidator,
    IValidator<UpdateNpiDraftCommand> updateValidator) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "AreaComercial")]
    public async Task<ActionResult<Guid>> Create(CreateNpiDraftRequest request, CancellationToken cancellationToken)
    {
        var user = User.ToCurrentUser();
        var command = new CreateNpiDraftCommand(user.UserId, user.Role, User.DisplayName(), request.Capture, request.Priority, request.PreAssignedEngineeringUserId);

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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new NpiListFilter(search, lifecycleStatus, assignedToUserId, createdByUserId, priority, page, pageSize);
        var result = await listHandler.HandleAsync(new ListNpisQuery(filter, User.ToCurrentUser()), cancellationToken);
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
}
