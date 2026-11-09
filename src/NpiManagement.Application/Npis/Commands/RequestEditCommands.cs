using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

// Editar o retirar una solicitud ya enviada (2026-11-04). Sólo el área que la pidió: Diseño en las
// solicitudes de información; Ingeniería o Diseño en sus solicitudes de cambios. A quien la iba a
// atender le llega un aviso (campana y correo) para que no trabaje con la versión vieja.

public sealed record EditInformationRequestCommand(Guid NpiId, Guid RequestId, CurrentUser Actor, string ActorName, string Text, IReadOnlyList<Guid>? AddImageAttachmentIds);

public sealed class EditInformationRequestCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(EditInformationRequestCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.EditInformationRequest(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.RequestId, command.Text, command.AddImageAttachmentIds);
        var request = npi.InformationRequests.First(r => r.Id == command.RequestId);
        NotifyRecipient(notifications, npi, request.Type, NotificationText.RequestEdited(npi.NpiNumber?.Value, command.Text));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }

    /// <summary>A quien le toca contestar: Comercial o el ingeniero asignado (las de "Cliente" ya no se crean).</summary>
    internal static void NotifyRecipient(INotificationService notifications, Npi npi, InformationRequestType type, string message)
    {
        if (type == InformationRequestType.Commercial)
            notifications.Notify(npi.AssignedCommercialUserId, message, npi.Id, NotificationChannel.BellAndEmail);
        else if (type == InformationRequestType.Engineering && npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, message, npi.Id, NotificationChannel.BellAndEmail);
    }
}

public sealed record WithdrawInformationRequestCommand(Guid NpiId, Guid RequestId, CurrentUser Actor, string ActorName);

public sealed class WithdrawInformationRequestCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(WithdrawInformationRequestCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.WithdrawInformationRequest(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.RequestId);
        var request = npi.InformationRequests.First(r => r.Id == command.RequestId);
        EditInformationRequestCommandHandler.NotifyRecipient(notifications, npi, request.Type, NotificationText.RequestWithdrawn(npi.NpiNumber?.Value, "Diseño"));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

public sealed record EditChangeRequestCommand(Guid NpiId, Guid RequestId, CurrentUser Actor, string ActorName, string Description, IReadOnlyList<string> FieldKeys, IReadOnlyDictionary<string, string>? FieldNotes, IReadOnlyList<Guid>? AddImageAttachmentIds);

public sealed class EditChangeRequestCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(EditChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        EnsureRequestingArea(npi, command.RequestId, command.Actor, command.NpiId);

        var request = npi.EditChangeRequest(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.RequestId,
            command.Description, command.FieldKeys, command.FieldNotes, command.AddImageAttachmentIds);
        notifications.Notify(npi.AssignedCommercialUserId, NotificationText.RequestEdited(npi.NpiNumber?.Value, request.Description), npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }

    /// <summary>Sólo el área que pidió los cambios puede tocarlos (404 si no, igual que el resto de acciones).</summary>
    internal static void EnsureRequestingArea(Npi npi, Guid requestId, CurrentUser actor, Guid npiId)
    {
        var request = npi.ChangeRequests.FirstOrDefault(c => c.Id == requestId) ?? throw new NotFoundException("ChangeRequest", requestId);
        var allowed = request.FromArea switch
        {
            AreaType.Ingenieria => NpiAuthorization.CanActAsEngineering(actor, npi.AssignedEngineeringUserId),
            AreaType.Diseno => NpiAuthorization.CanActAsDesign(actor, npi.AssignedDesignUserId),
            _ => false,
        };
        if (!allowed) throw new NotFoundException("Npi", npiId);
    }
}

public sealed record WithdrawChangeRequestCommand(Guid NpiId, Guid RequestId, CurrentUser Actor, string ActorName);

public sealed class WithdrawChangeRequestCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(WithdrawChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        EditChangeRequestCommandHandler.EnsureRequestingArea(npi, command.RequestId, command.Actor, command.NpiId);

        var request = npi.WithdrawChangeRequest(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.RequestId);
        var area = request.FromArea == AreaType.Ingenieria ? "Ingeniería" : "Diseño";
        notifications.Notify(npi.AssignedCommercialUserId, NotificationText.RequestWithdrawn(npi.NpiNumber?.Value, area), npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
