using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
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

        npi.ApproveEngineeringFeasibility(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Note, command.ConfirmedFieldKeys);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, $"Ingeniería aprobó la factibilidad del NPI {npi.NpiNumber?.Value}.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 8 de workflow.md — Factibilidad de Diseño (independiente de Ingeniería, decisión 1).</summary>
public sealed record ApproveDesignFeasibilityCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note, ArtClassification? ArtClassification, bool? HasLabValues = null);

public sealed class ApproveDesignFeasibilityCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ApproveDesignFeasibilityCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ApproveDesignFeasibility(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.Note, command.ArtClassification, command.HasLabValues);
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"Diseño aprobó la factibilidad del NPI {npi.NpiNumber?.Value}.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Diseño corrige la clasificación del arte / valores LAB que capturó al aprobar su factibilidad.</summary>
public sealed record UpdateDesignClassificationCommand(Guid NpiId, CurrentUser Actor, string ActorName, ArtClassification? ArtClassification, bool? HasLabValues);

public sealed class UpdateDesignClassificationCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(UpdateDesignClassificationCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.UpdateDesignClassification(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.ArtClassification, command.HasLabValues);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 9 de workflow.md — Ingeniería rechaza con solicitud de cambios a Comercial (no terminal).</summary>
public sealed record RequestEngineeringChangesCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Description, IReadOnlyList<string> FieldKeys, IReadOnlyDictionary<string, string>? FieldNotes = null, bool RedoPrintCard = false, IReadOnlyList<Guid>? ImageAttachmentIds = null);

public sealed class RequestEngineeringChangesCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RequestEngineeringChangesCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        var retiredBefore = npi.AuditLog.Count(e => e.Action == "PrintCardRetiredByEngineering");
        npi.RequestEngineeringChanges(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Description, command.FieldKeys, command.FieldNotes, command.RedoPrintCard, command.ImageAttachmentIds);
        notifications.Notify(npi.AssignedCommercialUserId, NotificationText.ChangesRequested(npi.NpiNumber?.Value, command.FieldKeys.Count, command.Description), npi.Id, NotificationChannel.BellAndEmail);
        // El cambio afecta el PrintCard: Diseño lo rehace cuando Comercial corrija.
        if (npi.AuditLog.Count(e => e.Action == "PrintCardRetiredByEngineering") > retiredBefore && npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId,
                $"Ingeniería pidió cambios a Comercial en el NPI {npi.NpiNumber?.Value} que afectan el PrintCard: cuando Comercial los corrija tendrás que hacer una versión nueva. Motivo: {command.Description}",
                npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 10 de workflow.md — Rechazo terminal por no factibilidad (sólo Ingeniería).</summary>
public sealed record RejectNoFeasibilityCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note);

/// <remarks>Un ingeniero (no admin) sólo lo propone: lo cierra AdminIngenieria o AdminTotal al dar el visto bueno.</remarks>
public sealed class RejectNoFeasibilityCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(RejectNoFeasibilityCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        await Closure.CloseOrProposeAsync(npi, TerminalReason.NoFeasibility, command.Actor, command.ActorName, command.Note, null,
            actor => npi.RejectNoFeasibility(actor, clock, calendar, command.Note), clock, notifications, userDirectory, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Diseño rechaza con solicitud de cambios a Comercial (no terminal; mismo ida y vuelta que Ingeniería).</summary>
public sealed record RequestDesignChangesCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Description, IReadOnlyList<string> FieldKeys, IReadOnlyDictionary<string, string>? FieldNotes = null, IReadOnlyList<Guid>? ImageAttachmentIds = null);

public sealed class RequestDesignChangesCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RequestDesignChangesCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        var retiredBefore = npi.AuditLog.Count(e => e.Action == "PrintCardRetiredByDesign");
        npi.RequestDesignChanges(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Description, command.FieldKeys, command.FieldNotes, command.ImageAttachmentIds);
        notifications.Notify(npi.AssignedCommercialUserId, NotificationText.DesignChangesRequested(npi.NpiNumber?.Value, command.FieldKeys.Count, command.Description), npi.Id, NotificationChannel.BellAndEmail);
        // Si Ingeniería tenía un PrintCard por revisar, ya no: Diseño lo va a rehacer.
        if (npi.AuditLog.Count(e => e.Action == "PrintCardRetiredByDesign") > retiredBefore && npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"Diseño pidió cambios a Comercial en el NPI {npi.NpiNumber?.Value} y retiró el PrintCard que estaba en revisión: se rehará con la información corregida.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Ingeniería revisa lo que Comercial cambió después de su factibilidad y confirma que sigue siendo factible.</summary>
public sealed record ReconfirmEngineeringFeasibilityCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note);

public sealed class ReconfirmEngineeringFeasibilityCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ReconfirmEngineeringFeasibilityCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ReconfirmEngineeringFeasibility(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Note);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, NotificationText.Reconfirmed(npi.NpiNumber?.Value, command.Note), npi.Id);
        notifications.Notify(npi.AssignedCommercialUserId, NotificationText.Reconfirmed(npi.NpiNumber?.Value, command.Note), npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Rechazo terminal de Diseño por no factibilidad (nota obligatoria).</summary>
public sealed record RejectDesignNoFeasibilityCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Note);

/// <remarks>Un diseñador (no admin) sólo lo propone: lo cierra AdminDiseno o AdminTotal al dar el visto bueno.</remarks>
public sealed class RejectDesignNoFeasibilityCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(RejectDesignNoFeasibilityCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        await Closure.CloseOrProposeAsync(npi, TerminalReason.DesignNoFeasibility, command.Actor, command.ActorName, command.Note, null,
            actor => npi.RejectDesignNoFeasibility(actor, clock, calendar, command.Note), clock, notifications, userDirectory, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
