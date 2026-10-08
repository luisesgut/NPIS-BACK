using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Queries;

public sealed record GetNpiCommentsQuery(Guid NpiId, CurrentUser Actor, int Page = 1, int PageSize = 20);

/// <summary>Mensajes del chat, del más reciente al más antiguo (el front los invierte para pintarlos).</summary>
public sealed class GetNpiCommentsQueryHandler(INpiChatStore chat)
{
    public async Task<PagedResult<CommentDto>> HandleAsync(GetNpiCommentsQuery query, CancellationToken cancellationToken)
    {
        var npi = await chat.FindNpiAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanAccess(query.Actor, npi.Ownership)) throw new NotFoundException("Npi", query.NpiId);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var result = await chat.ListAsync(npi.NpiId, page, pageSize, cancellationToken);
        return new PagedResult<CommentDto>(result.Items.Select(AddCommentCommandHandler.ToDto).ToList(), result.Page, result.PageSize, result.TotalCount);
    }
}

/// <summary>Persona a la que se puede mencionar en el chat. IsAssigned: creador o responsable del NPI (se listan primero).</summary>
public sealed record ChatParticipantDto(Guid UserId, string FullName, AreaType Area, bool IsAssigned);

public sealed record GetChatParticipantsQuery(Guid NpiId, CurrentUser Actor);

/// <summary>
/// Para el autocompletado de @: usuarios activos que pueden ver el NPI (los participantes y los
/// administradores). /api/users no sirve porque está reservado a administradores.
/// </summary>
public sealed class GetChatParticipantsQueryHandler(INpiChatStore chat, IUserDirectory userDirectory)
{
    public async Task<IReadOnlyList<ChatParticipantDto>> HandleAsync(GetChatParticipantsQuery query, CancellationToken cancellationToken)
    {
        var npi = await chat.FindNpiAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanAccess(query.Actor, npi.Ownership)) throw new NotFoundException("Npi", query.NpiId);

        var assigned = npi.ParticipantIds.ToHashSet();
        var users = await userDirectory.ListAsync(null, cancellationToken);

        return users
            .Where(u => u.Status == UserStatus.Active && NpiAuthorization.CanAccess(new CurrentUser(u.Id, u.Role, u.Area), npi.Ownership))
            .Select(u => new ChatParticipantDto(u.Id, u.FullName, u.Area, assigned.Contains(u.Id)))
            .OrderByDescending(p => p.IsAssigned)
            .ThenBy(p => p.FullName)
            .ToList();
    }
}

/// <summary>Mensajes sin leer del usuario actual, por NPI (para los contadores de listados y paneles).</summary>
public sealed class GetChatUnreadQueryHandler(INpiChatStore chat)
{
    public Task<IReadOnlyList<ChatUnreadDto>> HandleAsync(CurrentUser actor, CancellationToken cancellationToken) =>
        chat.GetUnreadAsync(actor.UserId, cancellationToken);
}

/// <summary>Usado por el hub de SignalR antes de unir una conexión al grupo de un NPI.</summary>
public sealed class ChatAccessQueryHandler(INpiChatStore chat)
{
    public async Task<bool> CanAccessAsync(CurrentUser actor, Guid npiId, CancellationToken cancellationToken)
    {
        var npi = await chat.FindNpiAsync(npiId, cancellationToken);
        return npi is not null && NpiAuthorization.CanAccess(actor, npi.Ownership);
    }
}
