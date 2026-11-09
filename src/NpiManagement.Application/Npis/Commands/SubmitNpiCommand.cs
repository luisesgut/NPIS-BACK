using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 3 de workflow.md — Enviar NPI (primer envío asigna folio; reenvío tras cambios reanuda Ingeniería).</summary>
/// <param name="Comment">Sólo al reenviar tras cambios: qué corrigió Comercial, para Ingeniería.</param>
public sealed record SubmitNpiCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Comment = null);

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
        // Las artes del cliente son opcionales al enviar: Comercial puede agregarlas después.
        var number = isFirstSubmission ? await numberGenerator.NextAsync(cancellationToken) : null;
        // Quién esperaba las correcciones (Ingeniería, Diseño o ambos) para avisarle sólo a ese.
        var engineeringWasWaiting = npi.EngineeringAreaStatus == EngineeringAreaStatus.ChangesRequested;
        var designWasWaiting = npi.DesignAreaStatus == DesignAreaStatus.ChangesRequested;
        var commercialEdit = npi.PendingCommercialEdit;
        var reconfirmationsBefore = npi.AuditLog.Count(e => e.Action == "EngineeringReconfirmationRequested");

        npi.Submit(actor, clock, calendar, number, command.Comment);

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
        else
        {
            var resolved = npi.ChangeRequests.Where(c => c.IsResolved).OrderByDescending(c => c.ResolvedAt).FirstOrDefault();
            var message = NotificationText.Resubmitted(npi.NpiNumber?.Value, resolved?.ChangesApplied.Count ?? 0, resolved?.ResponseComment);
            if (engineeringWasWaiting && npi.AssignedEngineeringUserId is { } engineerId2)
                notifications.Notify(engineerId2, message, npi.Id, NotificationChannel.BellAndEmail);
            if (designWasWaiting && npi.AssignedDesignUserId is { } designerId)
                notifications.Notify(designerId, message, npi.Id, NotificationChannel.BellAndEmail);

            // Comercial terminó de modificar el NPI por su cuenta: a Diseño (y a Ingeniería, si no le toca reconfirmar) les dice qué cambió.
            if (commercialEdit is not null)
            {
                var edited = $"Comercial modificó el NPI {npi.NpiNumber?.Value} ({commercialEdit.ChangesApplied.Count} campo{(commercialEdit.ChangesApplied.Count == 1 ? "" : "s")}). Motivo: {commercialEdit.Description}";
                if (npi.AssignedDesignUserId is { } designer)
                    notifications.Notify(designer, edited, npi.Id, NotificationChannel.BellAndEmail);
                if (npi.PendingEngineeringReconfirmation is null && npi.AssignedEngineeringUserId is { } engineer)
                    notifications.Notify(engineer, edited, npi.Id);
            }

            // Tocó campos que Ingeniería ya había aprobado: le toca reconfirmar.
            if (npi.AuditLog.Count(e => e.Action == "EngineeringReconfirmationRequested") > reconfirmationsBefore
                && npi.PendingEngineeringReconfirmation is { } reconfirmation && npi.AssignedEngineeringUserId is { } engineerId3)
                notifications.Notify(engineerId3,
                    NotificationText.ReconfirmationRequested(npi.NpiNumber?.Value, reconfirmation.ChangesApplied.Count, npi.PtRegisteredAt is not null, npi.EtCreatedAt is not null),
                    npi.Id, NotificationChannel.BellAndEmail);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
