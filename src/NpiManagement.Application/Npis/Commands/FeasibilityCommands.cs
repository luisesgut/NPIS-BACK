using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 7 de workflow.md — Factibilidad de Ingeniería (independiente de Diseño, decisión 1).</summary>
public sealed record ApproveEngineeringFeasibilityCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note, IReadOnlyList<string>? ConfirmedFieldKeys = null);

public sealed class ApproveEngineeringFeasibilityCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ApproveEngineeringFeasibilityCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ApproveEngineeringFeasibility(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.Note, command.ConfirmedFieldKeys);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, $"Ingeniería aprobó la factibilidad del NPI {npi.NpiNumber?.Value}.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 8 de workflow.md — Factibilidad de Diseño (independiente de Ingeniería, decisión 1).</summary>
public sealed record ApproveDesignFeasibilityCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note);

public sealed class ApproveDesignFeasibilityCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ApproveDesignFeasibilityCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ApproveDesignFeasibility(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.Note);
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"Diseño aprobó la factibilidad del NPI {npi.NpiNumber?.Value}.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 9 de workflow.md — Ingeniería rechaza con solicitud de cambios a Comercial (no terminal).</summary>
public sealed record RequestEngineeringChangesCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Description, IReadOnlyList<string> FieldKeys);

public sealed class RequestEngineeringChangesCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RequestEngineeringChangesCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RequestEngineeringChanges(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Description, command.FieldKeys);
        notifications.Notify(npi.AssignedCommercialUserId, $"El NPI {npi.NpiNumber?.Value} requiere cambios.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 10 de workflow.md — Rechazo terminal por no factibilidad (sólo Ingeniería).</summary>
public sealed record RejectNoFeasibilityCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note);

public sealed class RejectNoFeasibilityCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RejectNoFeasibilityCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RejectNoFeasibility(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Note);
        notifications.Notify(npi.AssignedCommercialUserId, $"El NPI {npi.NpiNumber?.Value} fue rechazado por no factibilidad.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
