using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Chat del NPI (antes "comentarios" — Fase 6, docs/api-contract.md §5; misma ruta e historial).
/// Permitido incluso en estado terminal. El tiempo real va por el hub /hubs/chat (NpiChatHub).
/// </summary>
[ApiController]
[Route("api/npis/{id:guid}/comments")]
[Authorize]
public sealed class NpiCommentsController(
    AddCommentCommandHandler addHandler,
    GetNpiCommentsQueryHandler listHandler,
    MarkChatReadCommandHandler markReadHandler,
    GetChatParticipantsQueryHandler participantsHandler) : ControllerBase
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
        var comment = await addHandler.HandleAsync(
            new AddCommentCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Text, request.MentionedUserIds),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, comment);
    }

    /// <summary>El usuario abrió (o tiene abierto) el chat: se marcan como leídos sus mensajes y su aviso agrupado.</summary>
    [HttpPost("read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await markReadHandler.HandleAsync(new MarkChatReadCommand(id, User.ToCurrentUser()), cancellationToken);
        return NoContent();
    }

    /// <summary>A quién se puede mencionar con @ en este NPI.</summary>
    [HttpGet("participants")]
    public async Task<ActionResult<IReadOnlyList<ChatParticipantDto>>> Participants(Guid id, CancellationToken cancellationToken) =>
        Ok(await participantsHandler.HandleAsync(new GetChatParticipantsQuery(id, User.ToCurrentUser()), cancellationToken));
}

/// <summary>Pendientes del chat del usuario actual en todos sus NPIs.</summary>
[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController(GetChatUnreadQueryHandler unreadHandler) : ControllerBase
{
    [HttpGet("unread")]
    public async Task<ActionResult<IReadOnlyList<ChatUnreadDto>>> Unread(CancellationToken cancellationToken) =>
        Ok(await unreadHandler.HandleAsync(User.ToCurrentUser(), cancellationToken));
}
