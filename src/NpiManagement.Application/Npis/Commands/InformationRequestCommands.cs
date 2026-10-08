using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 25 de workflow.md — Solicitar información (Diseño → Cliente o → Comercial).</summary>
public sealed record RequestInformationCommand(Guid NpiId, CurrentUser Actor, string ActorName, InformationRequestType Type, string Text);

public sealed class RequestInformationCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RequestInformationCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RequestInformation(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Type, command.Text);
        if (command.Type == InformationRequestType.Commercial)
            notifications.Notify(npi.AssignedCommercialUserId, NotificationText.InformationRequested(npi.NpiNumber?.Value, command.Text), npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 26 de workflow.md — Resolver solicitud de información del cliente (registrado por Diseño).</summary>
public sealed record ResolveClientInformationCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class ResolveClientInformationCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(ResolveClientInformationCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsDesign(command.Actor, npi.AssignedDesignUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.ResolveClientInformation(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 27 de workflow.md — Responder solicitud de información a Diseño (Comercial).</summary>
public sealed record RespondCommercialInformationCommand(Guid NpiId, CurrentUser Actor, string ActorName, string ResponseText);

public sealed class RespondCommercialInformationCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RespondCommercialInformationCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RespondCommercialInformation(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.ResponseText);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, NotificationText.InformationAnswered(npi.NpiNumber?.Value, command.ResponseText), npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
