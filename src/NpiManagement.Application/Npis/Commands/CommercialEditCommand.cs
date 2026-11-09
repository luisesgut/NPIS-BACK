using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Comercial modifica el NPI por su cuenta (p. ej. el cliente corrigió medidas), hasta antes del PT.
/// Después edita en el formulario y reenvía (SubmitNpiCommand). RedoPrintCard: el cambio afecta el
/// PrintCard y Diseño debe rehacerlo.
/// </summary>
public sealed record StartCommercialEditCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Reason, bool RedoPrintCard);

public sealed class StartCommercialEditCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<Dtos.NpiDetailDto> HandleAsync(StartCommercialEditCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanManageDraft(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        var retiredBefore = npi.AuditLog.Count(e => e.Action == "PrintCardRetiredByCommercial");
        npi.StartCommercialEdit(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Reason, command.RedoPrintCard);
        var printCardRetired = npi.AuditLog.Count(e => e.Action == "PrintCardRetiredByCommercial") > retiredBefore;

        // Diseño se entera siempre (y por correo si tendrá que rehacer el PrintCard); Ingeniería, por la campana.
        var text = $"Comercial está modificando el NPI {npi.NpiNumber?.Value}: {command.Reason.Trim()}";
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId,
                printCardRetired ? $"{text}. El cambio afecta el PrintCard: cuando lo reenvíe tendrás que hacer una versión nueva." : text,
                npi.Id, printCardRetired ? NotificationChannel.BellAndEmail : NotificationChannel.Bell);
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, text, npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
