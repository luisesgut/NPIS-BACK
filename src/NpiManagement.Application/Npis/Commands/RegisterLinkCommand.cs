using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Registra un link externo como adjunto del NPI (artes pesadas que no caben en el límite de
/// archivo). Misma lista y contextos que un archivo, así que el AttachmentId resultante también
/// sirve como evidencia en las acciones que la piden. Sólo Comercial y Diseño agregan links.
/// </summary>
public sealed record RegisterLinkCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Url, string? Title, AttachmentContext Context);

public sealed class RegisterLinkCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, INotificationService notifications, IUserDirectory? userDirectory = null)
{
    public const int MaxUrlLength = 2000;
    public const int MaxTitleLength = 200;

    public async Task<AttachmentDto> HandleAsync(RegisterLinkCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        var ownership = NpiAuthorization.OwnershipOf(npi);
        if (!NpiAuthorization.CanAccess(command.Actor, ownership)) throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi)
            && !NpiAuthorization.CanActAsDesign(command.Actor, npi))
            throw new NotFoundException("Npi", command.NpiId);

        var url = (command.Url ?? string.Empty).Trim();
        if (url.Length == 0 || url.Length > MaxUrlLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new RequestValidationException("link-invalid", "El link debe ser una dirección web completa que empiece con http:// o https://.");

        var title = (command.Title ?? string.Empty).Trim();
        if (title.Length == 0) title = uri.Host;
        if (title.Length > MaxTitleLength) title = title[..MaxTitleLength];

        var actor = new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName);
        var attachment = npi.RegisterLink(actor, clock, title, url, command.Context);
        await ArtworkNotifier.NotifyIfNeededAsync(notifications, userDirectory, npi, command.Context, command.Actor.UserId, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return NpiMapper.ToAttachmentDto(attachment);
    }
}
