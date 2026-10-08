using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Acción 28 de workflow.md — Mensaje en el chat del NPI (antes "comentario"). Permitido incluso en
/// estado terminal (registro histórico).
/// </summary>
public sealed record AddCommentCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Text, IReadOnlyCollection<Guid>? MentionedUserIds = null);

public sealed class AddCommentCommandHandler(
    INpiChatStore chat,
    IUserDirectory userDirectory,
    IUnitOfWork unitOfWork,
    IClock clock,
    INotificationService notifications,
    IChatRealtime realtime)
{
    public async Task<CommentDto> HandleAsync(AddCommentCommand command, CancellationToken cancellationToken)
    {
        var npi = await chat.FindNpiAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAccess(command.Actor, npi.Ownership)) throw new NotFoundException("Npi", command.NpiId);

        var author = new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName);
        var message = NpiComment.Create(npi.NpiId, author, clock, command.Text, command.MentionedUserIds);
        await EnsureMentionsCanSeeTheNpiAsync(message.MentionedUserIds, npi.Ownership, cancellationToken);

        chat.Add(message);
        // Quien escribe ya está viendo la conversación: su marca de leído avanza con su propio mensaje.
        await chat.MarkReadAsync(npi.NpiId, author.UserId, message.CreatedAt, cancellationToken);
        await notifications.MarkGroupReadAsync(author.UserId, NotificationText.ChatGroupKey(npi.NpiId), cancellationToken);

        // Antes cada comentario mandaba campana Y correo a todos los participantes: en un chat eso es
        // spam y la gente acaba filtrando el remitente. Ahora el correo es sólo para quien mencionan
        // (le piden algo a esa persona); al resto le queda un único aviso de campana por NPI.
        var mentioned = message.MentionedUserIds;
        notifications.NotifyMany(mentioned, NotificationText.Mention(npi.NpiNumber, author.Name, message.Text), npi.NpiId, NotificationChannel.BellAndEmail);

        var others = npi.ParticipantIds.Where(id => id != author.UserId && !mentioned.Contains(id)).ToList();
        foreach (var recipient in others)
            await notifications.NotifyGroupedAsync(recipient, NotificationText.ChatGroupKey(npi.NpiId), NotificationText.ChatMessage(npi.NpiNumber, author.Name, message.Text), npi.NpiId, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = ToDto(message);
        await realtime.MessagePostedAsync(dto, others.Concat(mentioned).ToList(), cancellationToken);
        return dto;
    }

    internal static CommentDto ToDto(NpiComment c) =>
        new(c.Id, c.NpiId, c.AuthorUserId, c.AuthorName, c.Text, c.CreatedAt, c.MentionedUserIds.ToList());

    /// <summary>
    /// Sólo se puede mencionar a alguien activo que ya pueda ver el NPI: una mención no da acceso, y
    /// avisar a alguien de un NPI que no puede abrir sólo le deja un link que termina en "no encontrado".
    /// </summary>
    private async Task EnsureMentionsCanSeeTheNpiAsync(IReadOnlyCollection<Guid> mentioned, NpiOwnership ownership, CancellationToken cancellationToken)
    {
        foreach (var userId in mentioned)
        {
            var user = await userDirectory.FindAsync(userId, cancellationToken);
            var canSee = user is { Status: UserStatus.Active }
                && NpiAuthorization.CanAccess(new CurrentUser(user.Id, user.Role, user.Area), ownership);
            if (!canSee)
                throw new RequestValidationException("chat-invalid-mention", "Sólo puedes mencionar a personas activas que tengan acceso a este NPI.");
        }
    }
}

/// <summary>Abrir el chat de un NPI: mueve la marca de leído y limpia su aviso agrupado de la campana.</summary>
public sealed record MarkChatReadCommand(Guid NpiId, CurrentUser Actor);

public sealed class MarkChatReadCommandHandler(INpiChatStore chat, IUnitOfWork unitOfWork, IClock clock, INotificationService notifications)
{
    public async Task HandleAsync(MarkChatReadCommand command, CancellationToken cancellationToken)
    {
        var npi = await chat.FindNpiAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAccess(command.Actor, npi.Ownership)) throw new NotFoundException("Npi", command.NpiId);

        await chat.MarkReadAsync(npi.NpiId, command.Actor.UserId, clock.UtcNow, cancellationToken);
        await notifications.MarkGroupReadAsync(command.Actor.UserId, NotificationText.ChatGroupKey(npi.NpiId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
