using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Application.Abstractions;

/// <summary>Lo mínimo del NPI que necesita el chat: folio para los avisos y propiedad para autorizar.</summary>
public sealed record NpiChatTarget(Guid NpiId, string? NpiNumber, NpiOwnership Ownership)
{
    /// <summary>Quienes participan en el NPI (creador y responsables de las tres áreas), sin repetir.</summary>
    public IReadOnlyList<Guid> ParticipantIds =>
        new Guid?[] { Ownership.CreatedByUserId, Ownership.AssignedCommercialUserId, Ownership.AssignedEngineeringUserId, Ownership.AssignedDesignUserId }
            .OfType<Guid>()
            .Distinct()
            .ToList();

    /// <summary>
    /// Escribir en el chat es sólo para los participantes. Los administradores que no participan
    /// pueden leer la conversación (ven todos los NPIs), pero no opinar en ella.
    /// </summary>
    public bool CanWrite(Guid userId) => ParticipantIds.Contains(userId);
}

/// <summary>Mensajes sin leer de un NPI para el usuario actual.</summary>
public sealed record ChatUnreadDto(Guid NpiId, string? NpiNumber, int UnreadCount, DateTimeOffset LastMessageAt);

/// <summary>
/// Persistencia del chat por NPI. Va aparte de <see cref="INpiRepository"/> a propósito: un mensaje
/// no toca el agregado, así que no se carga el NPI completo (con todo su historial) por cada mensaje.
/// Add y MarkReadAsync sólo preparan los cambios; los confirma <see cref="IUnitOfWork"/>.
/// </summary>
public interface INpiChatStore
{
    Task<NpiChatTarget?> FindNpiAsync(Guid npiId, CancellationToken cancellationToken);

    void Add(NpiComment message);

    /// <summary>Página de mensajes, del más reciente al más antiguo.</summary>
    Task<PagedResult<NpiComment>> ListAsync(Guid npiId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Mueve la marca "leído hasta" del usuario en ese NPI (nunca hacia atrás).</summary>
    Task MarkReadAsync(Guid npiId, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken);

    /// <summary>
    /// NPIs donde el usuario participa (creador o responsable) y tiene mensajes de otros sin leer.
    /// Los admins que no participan no acumulan pendientes de todos los NPIs: a ellos les llega la
    /// mención.
    /// </summary>
    Task<IReadOnlyList<ChatUnreadDto>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Aviso en tiempo real (SignalR, en Api). Se llama después de confirmar la transacción y nunca
/// debe hacer fallar la petición: si nadie está conectado o el envío falla, el mensaje ya quedó
/// guardado y se verá al recargar.
/// </summary>
public interface IChatRealtime
{
    /// <param name="message">Se envía a quienes tienen abierto el chat de ese NPI.</param>
    /// <param name="notifiedUserIds">Reciben un aviso ligero para refrescar campana y contadores, estén donde estén en la app.</param>
    Task MessagePostedAsync(CommentDto message, IReadOnlyCollection<Guid> notifiedUserIds, CancellationToken cancellationToken);
}
