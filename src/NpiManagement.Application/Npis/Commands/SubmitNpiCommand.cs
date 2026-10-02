using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 3 de workflow.md — Enviar NPI (primer envío asigna folio; reenvío tras cambios reanuda Ingeniería).</summary>
public sealed record SubmitNpiCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class SubmitNpiCommandHandler(
    INpiRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock,
    IBusinessCalendar calendar,
    INpiNumberGenerator numberGenerator,
    INotificationService notifications,
    IUserDirectory userDirectory)
{
    public async Task<Dtos.NpiDetailDto> HandleAsync(SubmitNpiCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken)
            ?? throw new NotFoundException("Npi", command.NpiId);

        if (!NpiAuthorization.CanManageDraft(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId))
            throw new NotFoundException("Npi", command.NpiId);

        var actor = new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName);
        var isFirstSubmission = npi.NpiNumber is null;
        var number = isFirstSubmission ? await numberGenerator.NextAsync(cancellationToken) : null;

        npi.Submit(actor, clock, calendar, number);

        if (isFirstSubmission)
        {
            if (npi.AssignedEngineeringUserId is { } engineerId)
                notifications.Notify(engineerId, $"Se te ha asignado el NPI {npi.NpiNumber?.Value}.", npi.Id, NotificationChannel.BellAndEmail);
            else
            {
                var ingAdmins = await userDirectory.ListAsync(AreaType.Ingenieria, cancellationToken);
                notifications.NotifyMany(
                    ingAdmins.Where(u => u.Role == "AdminIngenieria").Select(u => u.Id),
                    $"Nuevo NPI {npi.NpiNumber?.Value} disponible para asignar.", npi.Id, NotificationChannel.BellAndEmail);
            }

            var designAdmins = await userDirectory.ListAsync(AreaType.Diseno, cancellationToken);
            notifications.NotifyMany(
                designAdmins.Where(u => u.Role == "AdminDiseno").Select(u => u.Id),
                $"Nuevo NPI {npi.NpiNumber?.Value} disponible para Diseño en paralelo.", npi.Id, NotificationChannel.BellAndEmail);
        }
        else if (npi.AssignedEngineeringUserId is { } engineerId2)
        {
            notifications.Notify(engineerId2, $"El NPI {npi.NpiNumber?.Value} fue reenviado con los cambios solicitados.", npi.Id, NotificationChannel.BellAndEmail);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
