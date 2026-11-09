using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

// Etapa "Cotización SISPRO": se cotiza en SISPRO (cotizador oficial) y aquí Comercial liga el número
// y registra la respuesta del cliente al precio. Todas son acciones de Comercial.

/// <summary>Ligar una cotización de SISPRO: la lee, la compara contra la estructura del NPI y guarda la foto.</summary>
public sealed record LinkSisproQuoteCommand(Guid NpiId, CurrentUser Actor, string ActorName, string NumeroCotizacion);

public sealed class LinkSisproQuoteCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, IQuotationProvider quotations, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(LinkSisproQuoteCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        var numero = command.NumeroCotizacion?.Trim() ?? "";
        var quote = await quotations.FindAsync(numero, cancellationToken)
            ?? throw new RequestValidationException("sispro-quote-not-found", $"No existe la cotización {numero} en SISPRO.");
        if (!IsQuoted(quote.Estado))
            throw new RequestValidationException("sispro-quote-not-quoted", $"La cotización {numero} está «{quote.Estado}» en SISPRO: liga una que ya tenga precio.");

        var warnings = SisproStructureComparer.Compare(quote, npi.Capture);
        npi.LinkSisproQuote(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, quote.NumeroCotizacion, quote.Estado,
            NpiMapper.SerializeSnapshot(quote), warnings);

        // Si lo cotizado no es lo que Ingeniería aprobó, que Ingeniería se entere de inmediato.
        if (warnings.Count > 0 && npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"NPI {npi.NpiNumber?.Value}: la cotización SISPRO {quote.NumeroCotizacion} no coincide con la estructura aprobada ({warnings.Count} diferencia(s)).", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }

    // Mismos estados que SisproQuotationMapper.IsQuoted (Infrastructure): la cotización ya tiene precio.
    private static bool IsQuoted(string estado) =>
        estado.Trim().ToUpperInvariant() is "COTIZADA" or "ACEPTADA" or "CON PROPUESTA";
}

/// <summary>El cliente acepta el precio: arranca el PrintCard.</summary>
public sealed record AcceptSisproQuoteCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Note, Guid? EvidenceAttachmentId);

public sealed class AcceptSisproQuoteCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(AcceptSisproQuoteCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.AcceptSisproQuote(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Note, command.EvidenceAttachmentId);
        if (npi.AssignedDesignUserId is { } designerId)
            notifications.Notify(designerId, $"NPI {npi.NpiNumber?.Value}: el cliente aceptó el precio — ya puede iniciar el PrintCard.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>El cliente pide ajustar el precio: Comercial recotiza en SISPRO.</summary>
public sealed record RequestSisproQuoteAdjustmentCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Note);

public sealed class RequestSisproQuoteAdjustmentCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(RequestSisproQuoteAdjustmentCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RequestSisproQuoteAdjustment(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.Note);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Rechazo terminal por precio.</summary>
public sealed record RejectByPriceCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Comment, Guid? EvidenceAttachmentId);

/// <remarks>Un usuario comercial (no admin) sólo lo propone: lo cierra AdminComercial o AdminTotal al dar el visto bueno.</remarks>
public sealed class RejectByPriceCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(RejectByPriceCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        await Closure.CloseOrProposeAsync(npi, Domain.TerminalReason.PriceRejection, command.Actor, command.ActorName, command.Comment, command.EvidenceAttachmentId,
            actor => npi.RejectByPrice(actor, clock, calendar, command.Comment, command.EvidenceAttachmentId), clock, notifications, userDirectory, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
