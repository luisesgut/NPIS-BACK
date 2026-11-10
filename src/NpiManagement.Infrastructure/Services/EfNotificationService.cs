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
    // Lo que ya se avisó en esta petición: evita que el equipo reciba la misma copia dos veces
    // (p. ej. cuando el aviso va al creador y al comercial asignado, que son distintos).
    private readonly HashSet<(Guid Recipient, Guid? NpiId, string Message)> _added = [];

    public void Notify(Guid recipientUserId, string message, Guid? npiId, NotificationChannel channel = NotificationChannel.Bell)
    {
        AddBell(recipientUserId, message, npiId);

        // Sólo se apunta la intención. El envío real ocurre cuando la transacción confirma
        // (EfUnitOfWork) — aquí todavía no hay nada garantizado en base de datos.
        if (channel == NotificationChannel.BellAndEmail)
            pendingEmails.Add(new PendingEmailNotification(recipientUserId, message, npiId));

        CopyToTeam(recipientUserId, message, npiId);
    }

    private void AddBell(Guid recipientUserId, string message, Guid? npiId)
    {
        if (!_added.Add((recipientUserId, npiId, message))) return;
        dbContext.Notifications.Add(new NotificationEntity
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            NpiId = npiId,
            Message = message,
            CreatedAt = clock.UtcNow,
        });
    }

    /// <summary>
    /// Equipo por NPI (2026-11-10): lo que se le avisa al responsable de un área también le llega
    /// a su equipo de esa área, sólo en la campana (el correo es nada más para el responsable).
    /// El NPI ya está cargado por la acción que avisa; si no lo está, no hay copia.
    /// </summary>
    private void CopyToTeam(Guid recipientUserId, string message, Guid? npiId)
    {
        if (npiId is not { } id) return;
        var npi = dbContext.Npis.Local.FirstOrDefault(n => n.Id == id);
        if (npi is null || npi.Participants.Count == 0) return;

        foreach (var member in npi.Participants)
            if (member.UserId != recipientUserId && npi.IsResponsible(recipientUserId, member.Area))
                AddBell(member.UserId, message, npiId);
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

    public async Task MarkAllReadAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var unread = await dbContext.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && n.ReadAt == null)
            .ToListAsync(cancellationToken);
        if (unread.Count == 0) return;

        var now = clock.UtcNow;
        foreach (var notification in unread) notification.ReadAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
