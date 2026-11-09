using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Deshacer la última acción del NPI (2026-11-03): sólo quien la hizo y sólo si nadie más actuó
/// después (lo valida el dominio). Cubre también retirar una solicitud de cambios no atendida.
/// </summary>
public sealed record UndoLastActionCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class UndoLastActionCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(UndoLastActionCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(command.Actor, ownership)) throw new NotFoundException("Npi", command.NpiId);

        npi.UndoLastAction(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Corregir qué área pidió una solicitud de cambios pendiente. Sólo AdminTotal, con motivo.</summary>
public sealed record CorrectChangeRequestAreaCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid RequestId, AreaType Area, string Reason);

public sealed class CorrectChangeRequestAreaCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(CorrectChangeRequestAreaCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (command.Actor.Role != "AdminTotal") throw new NotFoundException("Npi", command.NpiId);

        npi.CorrectChangeRequestArea(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.RequestId, command.Area, command.Reason);

        // Quien ahora aparece como solicitante se entera (campana): la solicitud quedó a su nombre de área.
        var assignee = command.Area == AreaType.Diseno ? npi.AssignedDesignUserId : npi.AssignedEngineeringUserId;
        if (assignee is { } userId && userId != command.Actor.UserId)
            notifications.Notify(userId, $"La solicitud de cambios a Comercial del NPI {npi.NpiNumber?.Value} quedó a nombre de {(command.Area == AreaType.Diseno ? "Diseño" : "Ingeniería")}: {command.Reason.Trim()}", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Reabrir un NPI cerrado por error. Sólo AdminTotal, con motivo; se avisa a los responsables.</summary>
public sealed record ReopenNpiCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Reason);

public sealed class ReopenNpiCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ReopenNpiCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (command.Actor.Role != "AdminTotal") throw new NotFoundException("Npi", command.NpiId);

        npi.Reopen(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Reason);

        var recipients = new[] { (Guid?)npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId }
            .OfType<Guid>().Where(id => id != command.Actor.UserId).Distinct();
        notifications.NotifyMany(recipients, $"El NPI {npi.NpiNumber?.Value} se reabrió: {command.Reason.Trim()}", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
