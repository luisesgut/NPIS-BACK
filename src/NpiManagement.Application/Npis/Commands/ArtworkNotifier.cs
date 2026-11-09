using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Cuando se agregan artes (archivo o link, contexto Artwork) a un NPI ya enviado, se avisa al
/// diseñador asignado — típicamente el cliente las mandó después de levantar el NPI y Comercial
/// las sube desde el detalle. Si todavía no hay diseñador, el aviso va a los admins de Diseño, que
/// desde 2026-11-09 revisan el arte antes de asignar. Nunca se avisa a quien las agregó.
/// </summary>
internal static class ArtworkNotifier
{
    public static async Task NotifyIfNeededAsync(
        INotificationService notifications, IUserDirectory? userDirectory, Npi npi, AttachmentContext context, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (context != AttachmentContext.Artwork) return;
        if (npi.LifecycleStatus != LifecycleStatus.Active) return;

        if (npi.AssignedDesignUserId is { } designerId)
        {
            if (designerId != actorUserId)
                notifications.Notify(designerId, $"Se agregaron artes al NPI {npi.NpiNumber?.Value}.", npi.Id);
            return;
        }

        if (userDirectory is null) return;
        var designAdmin = NpiAuthorization.AdminRoleOf(AreaType.Diseno);
        var admins = (await userDirectory.ListAsync(null, cancellationToken))
            .Where(u => u.Status == UserStatus.Active && u.Id != actorUserId && (u.Role == designAdmin || u.Role == "AdminTotal"))
            .Select(u => u.Id);
        notifications.NotifyMany(admins, $"Se agregaron artes al NPI {npi.NpiNumber?.Value} (todavía sin diseñador asignado).", npi.Id);
    }
}
