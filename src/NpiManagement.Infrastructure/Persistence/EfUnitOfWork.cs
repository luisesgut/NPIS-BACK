using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Domain.Entities;
using NpiManagement.Infrastructure.Notifications;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    NpiManagementDbContext dbContext,
    PendingEmailNotifications pendingEmails,
    IEmailDispatchQueue emailQueue,
    IRealtimeEvents realtime,
    ILogger<EfUnitOfWork> logger) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Lo que se va a avisar en tiempo real se junta ANTES de guardar: después, EF ya no recuerda
        // qué cambió ni los responsables anteriores de un NPI reasignado.
        var changes = CollectChanges();

        var result = await dbContext.SaveChangesAsync(cancellationToken);

        // Éste es el único lugar del sistema donde consta que la acción de negocio realmente
        // ocurrió. Encolar aquí —y no en INotificationService.Notify— es lo que impide mandarle a
        // alguien un "se te asignó el NPI-1042" de una transacción que terminó en rollback.
        // Si SaveChanges lanza, esta línea no se alcanza y los avisos se descartan con el scope.
        foreach (var pending in pendingEmails.Drain())
            emailQueue.Enqueue(pending);

        await PublishAsync(changes);

        return result;
    }

    internal sealed record PendingChanges(Dictionary<Guid, HashSet<Guid>> Npis, HashSet<Guid> NotificationRecipients);

    internal PendingChanges CollectChanges()
    {
        var npis = new Dictionary<Guid, HashSet<Guid>>();
        var recipients = new HashSet<Guid>();

        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            switch (entry.Entity)
            {
                case Notification n:
                    recipients.Add(n.RecipientUserId);
                    break;
                // El chat tiene sus propios avisos (MessagePosted/ChatActivity); marcar leído no cambia el NPI.
                case NpiComment or NpiChatRead:
                    break;
                case Npi npi:
                    Participants(npis, npi.Id).UnionWith(OwnershipOf(entry));
                    break;
                default:
                    // Partes del NPI (solicitudes de cambio, adjuntos, eventos de cronómetro…): todas llevan NpiId.
                    if (entry.Metadata.FindProperty("NpiId") is not null && entry.Property("NpiId").CurrentValue is Guid npiId)
                        Participants(npis, npiId);
                    break;
            }
        }
        return new PendingChanges(npis, recipients);
    }

    private static HashSet<Guid> Participants(Dictionary<Guid, HashSet<Guid>> npis, Guid npiId)
    {
        if (!npis.TryGetValue(npiId, out var set)) npis[npiId] = set = [];
        return set;
    }

    /// <summary>Responsables actuales y, si se reasignó, también los anteriores (para que el NPI les desaparezca).</summary>
    private static IEnumerable<Guid> OwnershipOf(EntityEntry entry)
    {
        string[] properties = [nameof(Npi.CreatedByUserId), nameof(Npi.AssignedCommercialUserId), nameof(Npi.AssignedEngineeringUserId), nameof(Npi.AssignedDesignUserId)];
        foreach (var name in properties)
        {
            var property = entry.Property(name);
            if (property.CurrentValue is Guid current) yield return current;
            if (entry.State == EntityState.Modified && property.OriginalValue is Guid original) yield return original;
        }
    }

    private async Task PublishAsync(PendingChanges changes)
    {
        // Ya está guardado: un fallo aquí sólo significa que alguien verá el cambio al recargar o en
        // la siguiente consulta; nunca debe convertir la petición en un error.
        try
        {
            if (changes.Npis.Count > 0)
            {
                // Si sólo cambió una parte del NPI (p. ej. un adjunto), el NPI no estaba en el tracker: se piden sus responsables.
                var missing = changes.Npis.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
                if (missing.Count > 0)
                {
                    var owners = await dbContext.Npis.AsNoTracking()
                        .Where(n => missing.Contains(n.Id))
                        .Select(n => new { n.Id, n.CreatedByUserId, n.AssignedCommercialUserId, n.AssignedEngineeringUserId, n.AssignedDesignUserId })
                        .ToListAsync(CancellationToken.None);
                    foreach (var o in owners)
                        changes.Npis[o.Id].UnionWith(new Guid?[] { o.CreatedByUserId, o.AssignedCommercialUserId, o.AssignedEngineeringUserId, o.AssignedDesignUserId }.OfType<Guid>());
                }

                await realtime.NpisChangedAsync(changes.Npis.Select(kv => new NpiChange(kv.Key, kv.Value.ToList())).ToList());
            }

            if (changes.NotificationRecipients.Count > 0)
                await realtime.NotificationsChangedAsync(changes.NotificationRecipients.ToList());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron publicar los avisos en tiempo real");
        }
    }
}
