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
public sealed record CreatePrintCardVersionCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Code, Guid? AttachmentId, string? Note, IReadOnlyList<string>? CommercialChecklist = null);

public sealed class CreatePrintCardVersionCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(CreatePrintCardVersionCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.CreatePrintCardVersion(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Code, command.AttachmentId, command.Note, command.CommercialChecklist);

        // Le toca revisar a Ingeniería; Comercial se entera cuando Ingeniería la apruebe.
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"PrintCard {command.Code} (v{npi.CurrentPrintCardVersion?.VersionNumber}) listo para tu revisión en el NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);

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

        // Avisa a quien sigue: con Ingeniería, Comercial lo lleva al cliente; con el cliente, Diseño lo carga.
        if (command.Role == ApproverRole.Engineering)
            notifications.Notify(npi.AssignedCommercialUserId, $"Ingeniería aprobó el PrintCard del NPI {npi.NpiNumber?.Value}: ya puedes enviarlo al cliente.", npi.Id, NotificationChannel.BellAndEmail);
        if (npi.CurrentPrintCardVersion is { IsFullyApproved: true } && npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, $"El cliente aprobó el PrintCard del NPI {npi.NpiNumber?.Value}: cárgalo en sistema.", npi.Id, NotificationChannel.BellAndEmail);

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
public sealed record RequestClientChangesCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Description, IReadOnlyList<string> Items, IReadOnlyList<Guid>? AttachmentIds = null);

public sealed class RequestClientChangesCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RequestClientChangesCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RequestClientChanges(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Description, command.Items, command.AttachmentIds);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, $"El cliente solicitó {command.Items.Count(i => !string.IsNullOrWhiteSpace(i))} cambio(s) al PrintCard del NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Diseño palomea (o despalomea) un cambio que pidió el cliente sobre el PrintCard.</summary>
public sealed record SetClientChangeItemCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid ItemId, bool Done, string? Comment = null);

public sealed class SetClientChangeItemCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(SetClientChangeItemCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.SetClientChangeItemDone(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.ItemId, command.Done, command.Comment);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Ingeniería rechaza la versión vigente del PrintCard con motivo: Diseño genera una nueva.</summary>
public sealed record RejectPrintCardCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Reason, Guid? EvidenceAttachmentId);

public sealed class RejectPrintCardCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RejectPrintCardCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RejectPrintCard(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Reason, command.EvidenceAttachmentId);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, NotificationText.PrintCardRejected(npi.NpiNumber?.Value, npi.CurrentPrintCardVersion?.VersionNumber, command.Reason), npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 16 de workflow.md — Rechazo definitivo del cliente (terminal, decisión 7). Siempre lo ejecuta Comercial (decisión 8).</summary>
public sealed record RejectByClientCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Comment, Guid? EvidenceAttachmentId);

/// <remarks>Un usuario comercial (no admin) sólo lo propone: lo cierra AdminComercial o AdminTotal al dar el visto bueno.</remarks>
public sealed class RejectByClientCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(RejectByClientCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        await Closure.CloseOrProposeAsync(npi, TerminalReason.ClientRejection, command.Actor, command.ActorName, command.Comment, command.EvidenceAttachmentId,
            actor => npi.RejectByClient(actor, clock, calendar, command.Comment, command.EvidenceAttachmentId), clock, notifications, userDirectory, cancellationToken);

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

/// <summary>Diseño rechaza las artes del cliente (motivo obligatorio, evidencia opcional). Regresa a Comercial.</summary>
public sealed record RejectArtworkCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Reason, Guid? EvidenceAttachmentId);

public sealed class RejectArtworkCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RejectArtworkCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RejectArtwork(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Reason, command.EvidenceAttachmentId);
        notifications.Notify(npi.AssignedCommercialUserId, NotificationText.ArtworkRejected(npi.NpiNumber?.Value, command.Reason), npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Comercial palomea una observación del rechazo de artes.</summary>
public sealed record SetArtworkObservationCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid ItemId, bool Done, string? Comment = null);

public sealed class SetArtworkObservationCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(SetArtworkObservationCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.SetArtworkObservationDone(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.ItemId, command.Done, command.Comment);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Comercial palomea un punto que Diseño le pidió revisar con el cliente en el PrintCard vigente.</summary>
public sealed record SetPrintCardObservationCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid ItemId, bool Done, string? Comment = null);

public sealed class SetPrintCardObservationCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(SetPrintCardObservationCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.SetPrintCardObservationDone(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.ItemId, command.Done, command.Comment);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Comercial avisa que ya subió las artes corregidas; regresa a Diseño.</summary>
public sealed record ResolveArtworkRejectionCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Comment);

public sealed class ResolveArtworkRejectionCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ResolveArtworkRejectionCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ResolveArtworkRejection(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Comment);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, NotificationText.ArtworkResubmitted(npi.NpiNumber?.Value, command.Comment), npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Diseño reemplaza el archivo del PrintCard vigente antes de que Ingeniería lo revise (2026-11-09).</summary>
public sealed record ReplacePrintCardFileCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid AttachmentId);

public sealed class ReplacePrintCardFileCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ReplacePrintCardFileCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ReplacePrintCardFile(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.AttachmentId);
        // A Ingeniería le toca revisarlo: que vea el archivo nuevo y no el que ya había abierto.
        if (npi.AssignedEngineeringUserId is { } engineerId && npi.CurrentPrintCardVersion is { } version)
            notifications.Notify(engineerId, $"Diseño reemplazó el archivo del PrintCard v{version.VersionNumber} del NPI {npi.NpiNumber?.Value}: revisa el nuevo.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
