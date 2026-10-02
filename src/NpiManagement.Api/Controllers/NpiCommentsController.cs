using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;

namespace NpiManagement.Api.Controllers;

/// <summary>Comentarios del NPI — Fase 6 (docs/api-contract.md §5). Permitido incluso en estado terminal.</summary>
[ApiController]
[Route("api/npis/{id:guid}/comments")]
[Authorize]
public sealed class NpiCommentsController(AddCommentCommandHandler addHandler, GetNpiCommentsQueryHandler listHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CommentDto>>> List(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await listHandler.HandleAsync(new GetNpiCommentsQuery(id, User.ToCurrentUser(), page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Add(Guid id, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await addHandler.HandleAsync(new AddCommentCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Text), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, comment);
    }
}
