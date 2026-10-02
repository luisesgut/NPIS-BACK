using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 22 de workflow.md — Rechazo temporal por falta de insumos (Ingeniería, no terminal).</summary>
public sealed record RejectSuppliesTemporaryCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note);

public sealed class RejectSuppliesTemporaryCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RejectSuppliesTemporaryCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RejectSuppliesTemporary(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Note);
        notifications.Notify(npi.AssignedCommercialUserId, $"NPI {npi.NpiNumber?.Value}: rechazo temporal por falta de insumos — requiere acuerdo de Comercial.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 23 de workflow.md — Acuerdo para solventar insumos (Comercial). Reactiva Ingeniería.</summary>
public sealed record ResolveSuppliesAgreementCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Text, Guid EvidenceAttachmentId);

public sealed class ResolveSuppliesAgreementCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ResolveSuppliesAgreementCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ResolveSuppliesAgreement(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Text, command.EvidenceAttachmentId);
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"NPI {npi.NpiNumber?.Value}: acuerdo de insumos registrado — puede continuar.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 24 de workflow.md — Rechazo total por falta de insumos (terminal).</summary>
public sealed record RejectSuppliesTotalCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class RejectSuppliesTotalCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RejectSuppliesTotalCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RejectSuppliesTotal(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar);

        var recipients = new List<Guid>();
        if (npi.AssignedEngineeringUserId is { } engineerId) recipients.Add(engineerId);
        if (npi.AssignedDesignUserId is { } designerId) recipients.Add(designerId);
        notifications.NotifyMany(recipients, $"NPI {npi.NpiNumber?.Value} cerrado por rechazo total de insumos.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
