using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 28 de workflow.md — Agregar comentario. Permitido incluso en estado terminal (registro histórico).</summary>
public sealed record AddCommentCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Text);

public sealed class AddCommentCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<CommentDto> HandleAsync(AddCommentCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(command.Actor, ownership)) throw new NotFoundException("Npi", command.NpiId);

        var comment = npi.AddComment(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.Text);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CommentDto(comment.Id, comment.AuthorUserId, comment.AuthorName, comment.Text, comment.CreatedAt);
    }
}
