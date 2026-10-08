using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Notifications;
using NpiManagement.Infrastructure.Persistence;
using NotificationEntity = NpiManagement.Infrastructure.Persistence.Entities.Notification;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfNotificationService(
    NpiManagementDbContext dbContext,
    PendingEmailNotifications pendingEmails,
    IClock clock) : INotificationService
{
    public void Notify(Guid recipientUserId, string message, Guid? npiId, NotificationChannel channel = NotificationChannel.Bell)
    {
        dbContext.Notifications.Add(new NotificationEntity
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            NpiId = npiId,
            Message = message,
            CreatedAt = clock.UtcNow,
        });

        // Sólo se apunta la intención. El envío real ocurre cuando la transacción confirma
        // (EfUnitOfWork) — aquí todavía no hay nada garantizado en base de datos.
        if (channel == NotificationChannel.BellAndEmail)
            pendingEmails.Add(new PendingEmailNotification(recipientUserId, message, npiId));
    }

    public void NotifyMany(IEnumerable<Guid> recipientUserIds, string message, Guid? npiId, NotificationChannel channel = NotificationChannel.Bell)
    {
        foreach (var recipientUserId in recipientUserIds.Distinct())
            Notify(recipientUserId, message, npiId, channel);
    }

    public async Task NotifyGroupedAsync(Guid recipientUserId, string groupKey, string message, Guid? npiId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.RecipientUserId == recipientUserId && n.GroupKey == groupKey && n.ReadAt == null, cancellationToken);

        if (existing is null)
        {
            dbContext.Notifications.Add(new NotificationEntity
            {
                Id = Guid.NewGuid(),
                RecipientUserId = recipientUserId,
                NpiId = npiId,
                Message = message,
                CreatedAt = clock.UtcNow,
                GroupKey = groupKey,
            });
            return;
        }

        // Se reescribe con el último mensaje y sube al principio de la campana.
        existing.Message = message;
        existing.CreatedAt = clock.UtcNow;
    }

    public async Task MarkGroupReadAsync(Guid recipientUserId, string groupKey, CancellationToken cancellationToken)
    {
        var unread = await dbContext.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && n.GroupKey == groupKey && n.ReadAt == null)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        foreach (var notification in unread)
            notification.ReadAt = now;
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid recipientUserId, bool unreadOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.Notifications.AsNoTracking().Where(n => n.RecipientUserId == recipientUserId);
        if (unreadOnly) query = query.Where(n => n.ReadAt == null);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Message, n.NpiId, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);

        return notifications;
    }

    public async Task MarkReadAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == recipientUserId, cancellationToken);
        if (notification is null || notification.ReadAt is not null) return;

        notification.ReadAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
