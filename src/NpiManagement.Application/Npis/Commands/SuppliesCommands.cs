using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
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
/// <remarks>Un usuario comercial (no admin) sólo lo propone (nota obligatoria): lo cierra AdminComercial o AdminTotal.</remarks>
public sealed record RejectSuppliesTotalCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note = null);

public sealed class RejectSuppliesTotalCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(RejectSuppliesTotalCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        await Closure.CloseOrProposeAsync(npi, TerminalReason.SuppliesRejectionTotal, command.Actor, command.ActorName, command.Note, null,
            actor => npi.RejectSuppliesTotal(actor, clock, calendar, command.Note), clock, notifications, userDirectory, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
