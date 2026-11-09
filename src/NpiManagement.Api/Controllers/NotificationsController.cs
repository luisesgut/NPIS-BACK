using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Api.Controllers;

/// <summary>Notificaciones propias del usuario autenticado (docs/api-contract.md §10).</summary>
[ApiController]
[Route("api/notifications")]
[AllowForReadOnlyRole]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly, CancellationToken cancellationToken)
    {
        var notifications = await notificationService.ListAsync(User.ToCurrentUser().UserId, unreadOnly, cancellationToken);
        return Ok(notifications);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await notificationService.MarkReadAsync(id, User.ToCurrentUser().UserId, cancellationToken);
        return NoContent();
    }

    /// <summary>Marca como leídas todas las notificaciones del usuario.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notificationService.MarkAllReadAsync(User.ToCurrentUser().UserId, cancellationToken);
        return NoContent();
    }
}
