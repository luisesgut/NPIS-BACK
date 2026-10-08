using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 11 de workflow.md — Artes Validadas (Diseño).</summary>
public sealed record ValidateArtworkCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class ValidateArtworkCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(ValidateArtworkCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ValidateArtwork(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 12 de workflow.md — Crear versión de PrintCard (exige las 4 condiciones de la decisión 2).</summary>
public sealed record CreatePrintCardVersionCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Code, Guid? AttachmentId, string? Note);

public sealed class CreatePrintCardVersionCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(CreatePrintCardVersionCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.CreatePrintCardVersion(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Code, command.AttachmentId, command.Note);

        notifications.Notify(npi.AssignedCommercialUserId, $"PrintCard {command.Code} listo para aprobación en el NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"PrintCard {command.Code} listo para aprobación en el NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 13 de workflow.md — Aprobar PrintCard vigente. Rol "Client" siempre lo ejecuta Comercial (decisión 8).</summary>
public sealed record ApprovePrintCardCommand(Guid NpiId, CurrentUser Actor, string ActorName, ApproverRole Role, string? Note, Guid? EvidenceAttachmentId);

public sealed class ApprovePrintCardCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ApprovePrintCardCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);

        var authorized = command.Role switch
        {
            ApproverRole.Design => NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId),
            ApproverRole.Engineering => NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId),
            ApproverRole.Client => NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId),
            _ => false,
        };
        if (!authorized) throw new NotFoundException("Npi", command.NpiId);

        npi.ApprovePrintCard(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Role, command.Note, command.EvidenceAttachmentId);

        var version = npi.CurrentPrintCardVersion;
        if (version is not null && version.IsFullyApproved && npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"PrintCard del NPI {npi.NpiNumber?.Value} autorizado — puede proceder con el Alta SAP.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 14 de workflow.md — Registrar el envío del PrintCard vigente al cliente (informativo).</summary>
public sealed record SendPrintCardToClientCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class SendPrintCardToClientCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(SendPrintCardToClientCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.SendPrintCardToClient(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 15 de workflow.md — Cliente solicita cambios (registrado por Comercial, no terminal).</summary>
public sealed record RequestClientChangesCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Description);

public sealed class RequestClientChangesCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RequestClientChangesCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RequestClientChanges(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Description);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, $"El cliente solicitó cambios al PrintCard del NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 16 de workflow.md — Rechazo definitivo del cliente (terminal, decisión 7). Siempre lo ejecuta Comercial (decisión 8).</summary>
public sealed record RejectByClientCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Comment, Guid? EvidenceAttachmentId);

public sealed class RejectByClientCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RejectByClientCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RejectByClient(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Comment, command.EvidenceAttachmentId);

        var recipients = new List<Guid>();
        if (npi.AssignedEngineeringUserId is { } engineerId) recipients.Add(engineerId);
        if (npi.AssignedDesignUserId is { } designerId) recipients.Add(designerId);
        notifications.NotifyMany(recipients, $"El cliente rechazó definitivamente el NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 17 de workflow.md — PrintCard cargado en Sistema. No es terminal; sólo cierra el timer de Diseño (decisión 3).</summary>
public sealed record LoadPrintCardIntoSystemCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class LoadPrintCardIntoSystemCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(LoadPrintCardIntoSystemCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.LoadPrintCardIntoSystem(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar);
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"PrintCard del NPI {npi.NpiNumber?.Value} cargado en sistema — puede registrar el PT en SAP.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
