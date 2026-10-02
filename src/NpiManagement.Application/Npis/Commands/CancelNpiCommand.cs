using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 29 de workflow.md — Cancelar (administrativo, ADR-024). Sólo AdminComercial/AdminTotal.</summary>
public sealed record CancelNpiCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Reason);

public sealed class CancelNpiCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(CancelNpiCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (command.Actor.Role is not ("AdminTotal" or "AdminComercial")) throw new NotFoundException("Npi", command.NpiId);

        npi.Cancel(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Reason);

        var recipients = new List<Guid> { npi.AssignedCommercialUserId };
        if (npi.AssignedEngineeringUserId is { } engineerId) recipients.Add(engineerId);
        if (npi.AssignedDesignUserId is { } designerId) recipients.Add(designerId);
        notifications.NotifyMany(recipients, $"El NPI {npi.NpiNumber?.Value} fue cancelado: {command.Reason}", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
