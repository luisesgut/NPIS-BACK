using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Indicaciones del admin de Diseño para el diseñador (2026-11-09). Al diseñador asignado le llega aviso en la campana.</summary>
public sealed record SetDesignInstructionsCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Text);

public sealed class SetDesignInstructionsCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(SetDesignInstructionsCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAssign(command.Actor, AreaType.Diseno)) throw new NotFoundException("Npi", command.NpiId);

        npi.SetDesignInstructions(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.Text);
        if (npi.DesignInstructions is not null && npi.AssignedDesignUserId is { } designerId && designerId != command.Actor.UserId)
            notifications.Notify(designerId, $"El admin de Diseño dejó indicaciones para el NPI {npi.NpiNumber?.Value}: revisa la etapa de factibilidad de Diseño.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>
/// Eliminar un adjunto subido por error (2026-11-09): quien lo subió o AdminTotal, y sólo si no es
/// evidencia de nada. Sólo sale de la lista del NPI: el archivo se queda en el servidor (se puede
/// recuperar con la clave que guarda la bitácora).
/// </summary>
public sealed record DeleteAttachmentCommand(Guid NpiId, Guid AttachmentId, CurrentUser Actor, string ActorName);

public sealed class DeleteAttachmentCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task HandleAsync(DeleteAttachmentCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(command.Actor, ownership)) throw new NotFoundException("Npi", command.NpiId);

        npi.RemoveAttachment(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.AttachmentId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
