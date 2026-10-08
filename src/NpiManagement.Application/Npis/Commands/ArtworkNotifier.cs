using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Cuando se agregan artes (archivo o link, contexto Artwork) a un NPI ya enviado, se avisa al
/// diseñador asignado — típicamente el cliente las mandó después de levantar el NPI y Comercial
/// las sube desde Adjuntos. No se avisa si quien las agrega es el propio diseñador.
/// </summary>
internal static class ArtworkNotifier
{
    public static void NotifyIfNeeded(INotificationService notifications, Npi npi, AttachmentContext context, Guid actorUserId)
    {
        if (context != AttachmentContext.Artwork) return;
        if (npi.LifecycleStatus != LifecycleStatus.Active) return;
        if (npi.AssignedDesignUserId is not { } designerId || designerId == actorUserId) return;

        notifications.Notify(designerId, $"Se agregaron artes al NPI {npi.NpiNumber?.Value}.", npi.Id);
    }
}
