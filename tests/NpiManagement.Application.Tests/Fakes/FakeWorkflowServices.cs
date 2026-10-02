using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Application.Tests.Fakes;

public sealed class FakeNpiNumberGenerator : INpiNumberGenerator
{
    private int _next = 1;

    public Task<NpiNumber> NextAsync(CancellationToken cancellationToken) => Task.FromResult(NpiNumber.FromSequenceValue(_next++));
}

/// <summary>Se guarda también el canal para poder afirmar en pruebas qué eventos salen por correo y cuáles se quedan en la campana.</summary>
public sealed record SentNotification(Guid RecipientUserId, string Message, Guid? NpiId, NotificationChannel Channel);

public sealed class FakeNotificationService : INotificationService
{
    public List<SentNotification> Sent { get; } = [];

    public void Notify(Guid recipientUserId, string message, Guid? npiId, NotificationChannel channel = NotificationChannel.Bell) =>
        Sent.Add(new SentNotification(recipientUserId, message, npiId, channel));

    public void NotifyMany(IEnumerable<Guid> recipientUserIds, string message, Guid? npiId, NotificationChannel channel = NotificationChannel.Bell)
    {
        foreach (var id in recipientUserIds) Notify(id, message, npiId, channel);
    }

    public Task<IReadOnlyList<NotificationDto>> ListAsync(Guid recipientUserId, bool unreadOnly, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NotificationDto>>([]);

    public Task MarkReadAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FakeUserDirectory : IUserDirectory
{
    public List<UserSummaryDto> Users { get; } = [];

    public Task<IReadOnlyList<UserSummaryDto>> ListAsync(AreaType? area, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserSummaryDto>>(area is null ? Users : Users.Where(u => u.Area == area).ToList());

    public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u.FullName));
}
