using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;

namespace NpiManagement.Api.Realtime;

/// <summary>
/// Canal en tiempo real del chat. Sólo empuja: los mensajes se escriben por REST
/// (POST /api/npis/{id}/comments), que valida, guarda y después avisa por aquí — un único camino de
/// escritura. Cada conexión se une al grupo del NPI que tiene abierto; además, SignalR la asocia a
/// su usuario para los avisos ligeros ("ChatActivity") que refrescan campana y contadores.
///
/// Eventos que recibe el cliente:
///  - MessagePosted(CommentDto) — a quienes tienen abierto ese NPI.
///  - ChatActivity({ npiId })    — a los participantes y mencionados, estén donde estén.
///  - NpiChanged({ npiIds })     — algo cambió en esos NPIs (estatus, cronómetros, asignación, adjuntos…).
///  - NotificationsChanged()    — llegó o se leyó una notificación del usuario.
/// </summary>
[Authorize]
public sealed class NpiChatHub(ChatAccessQueryHandler access) : Hub
{
    public const string Path = "/hubs/chat";

    public static string GroupFor(Guid npiId) => $"npi-{npiId:N}";

    /// <summary>Administradores: ven todos los NPIs, así que reciben el aviso de cualquier cambio.</summary>
    public const string AdminsGroup = "admins";

    public override async Task OnConnectedAsync()
    {
        if (NpiAuthorization.SeesAllNpis(Context.User!.ToCurrentUser()))
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>Misma autorización que el detalle del NPI; si no puede verlo, se responde igual que si no existiera.</summary>
    public async Task JoinNpi(Guid npiId)
    {
        if (!await access.CanAccessAsync(Context.User!.ToCurrentUser(), npiId, Context.ConnectionAborted))
            throw new HubException("El NPI no existe o no está disponible para el usuario actual.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(npiId));
    }

    public Task LeaveNpi(Guid npiId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(npiId));
}

/// <summary>Identifica cada conexión con el mismo id de usuario que usa ToCurrentUser (claim NameIdentifier o sub).</summary>
public sealed class UserIdFromTokenProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var raw = connection.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? connection.User?.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? Key(id) : null;
    }

    public static string Key(Guid userId) => userId.ToString("N");
}

public sealed class SignalRChatRealtime(IHubContext<NpiChatHub> hub, ILogger<SignalRChatRealtime> logger) : IChatRealtime
{
    public async Task MessagePostedAsync(CommentDto message, IReadOnlyCollection<Guid> notifiedUserIds, CancellationToken cancellationToken)
    {
        // El mensaje ya está guardado: un fallo aquí sólo significa que alguien lo verá al recargar
        // (o con la consulta de respaldo del front), nunca debe convertir la petición en un error.
        // Tampoco se usa el token de la petición: si el cliente cierra la conexión justo después de
        // enviar, el aviso a los demás debe salir igual.
        try
        {
            await hub.Clients.Group(NpiChatHub.GroupFor(message.NpiId)).SendAsync("MessagePosted", message, CancellationToken.None);

            if (notifiedUserIds.Count > 0)
            {
                var users = notifiedUserIds.Select(UserIdFromTokenProvider.Key).ToList();
                await hub.Clients.Users(users).SendAsync("ChatActivity", new { npiId = message.NpiId }, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo avisar en tiempo real del mensaje {MessageId} del NPI {NpiId}", message.Id, message.NpiId);
        }
    }
}

/// <summary>Avisos de cambios de NPIs y notificaciones (ver <see cref="IRealtimeEvents"/>). Sólo ids: cada cliente vuelve a consultar.</summary>
public sealed class SignalRRealtimeEvents(IHubContext<NpiChatHub> hub, ILogger<SignalRRealtimeEvents> logger) : IRealtimeEvents
{
    public async Task NpisChangedAsync(IReadOnlyCollection<NpiChange> changes)
    {
        try
        {
            var allIds = changes.Select(c => c.NpiId).ToList();
            await hub.Clients.Group(NpiChatHub.AdminsGroup).SendAsync("NpiChanged", new { npiIds = allIds });

            // Un aviso por usuario con sólo los NPIs en los que participa.
            var byUser = changes
                .SelectMany(c => c.ParticipantIds.Select(userId => (userId, c.NpiId)))
                .GroupBy(x => x.userId, x => x.NpiId);
            foreach (var group in byUser)
                await hub.Clients.User(UserIdFromTokenProvider.Key(group.Key)).SendAsync("NpiChanged", new { npiIds = group.Distinct().ToList() });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo avisar en tiempo real del cambio de {Count} NPI(s)", changes.Count);
        }
    }

    public async Task NotificationsChangedAsync(IReadOnlyCollection<Guid> userIds)
    {
        try
        {
            await hub.Clients.Users(userIds.Select(UserIdFromTokenProvider.Key).ToList()).SendAsync("NotificationsChanged");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo avisar en tiempo real de notificaciones nuevas");
        }
    }
}
