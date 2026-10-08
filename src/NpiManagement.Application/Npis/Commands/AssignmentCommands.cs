using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 4 de workflow.md — Asignar (o reasignar) a Ingeniería. Sólo AdminIngenieria/AdminTotal.</summary>
public sealed record AssignEngineeringCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid UserId);

public sealed class AssignEngineeringCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(AssignEngineeringCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAssign(command.Actor, AreaType.Ingenieria)) throw new NotFoundException("Npi", command.NpiId);

        await AssigneeGuard.EnsureAssignableAsync(userDirectory, command.UserId, AreaType.Ingenieria, cancellationToken);
        npi.AssignEngineering(command.UserId, new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);
        notifications.Notify(command.UserId, $"Se te ha asignado el NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 5 de workflow.md — Asignar (o reasignar) a Diseño. Sólo AdminDiseno/AdminTotal.</summary>
public sealed record AssignDesignCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid UserId);

public sealed class AssignDesignCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(AssignDesignCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAssign(command.Actor, AreaType.Diseno)) throw new NotFoundException("Npi", command.NpiId);

        await AssigneeGuard.EnsureAssignableAsync(userDirectory, command.UserId, AreaType.Diseno, cancellationToken);
        npi.AssignDesign(command.UserId, new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);
        notifications.Notify(command.UserId, $"Se te ha asignado el NPI {npi.NpiNumber?.Value} para diseño de PrintCard.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 6 de workflow.md — Reasignar Comercial. Sólo AdminComercial/AdminTotal.</summary>
public sealed record ReassignCommercialCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid UserId);

public sealed class ReassignCommercialCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(ReassignCommercialCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAssign(command.Actor, AreaType.Comercial)) throw new NotFoundException("Npi", command.NpiId);

        await AssigneeGuard.EnsureAssignableAsync(userDirectory, command.UserId, AreaType.Comercial, cancellationToken);
        npi.ReassignCommercial(command.UserId, new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);
        notifications.Notify(command.UserId, $"Se te ha reasignado el NPI {npi.NpiNumber?.Value} (Comercial).", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
