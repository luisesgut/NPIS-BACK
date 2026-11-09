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

    /// <summary>Avisos agrupados vigentes: uno por (destinatario, clave), reescrito con cada aviso nuevo como en EF.</summary>
    public Dictionary<(Guid RecipientUserId, string GroupKey), string> Grouped { get; } = [];

    public List<(Guid RecipientUserId, string GroupKey)> GroupsMarkedRead { get; } = [];

    public Task NotifyGroupedAsync(Guid recipientUserId, string groupKey, string message, Guid? npiId, CancellationToken cancellationToken)
    {
        Grouped[(recipientUserId, groupKey)] = message;
        return Task.CompletedTask;
    }

    public Task MarkGroupReadAsync(Guid recipientUserId, string groupKey, CancellationToken cancellationToken)
    {
        Grouped.Remove((recipientUserId, groupKey));
        GroupsMarkedRead.Add((recipientUserId, groupKey));
        return Task.CompletedTask;
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

    public Task<UserSummaryDto?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

    /// <summary>Agrega un usuario activo del área indicada (las asignaciones exigen uno).</summary>
    public FakeUserDirectory With(Guid id, AreaType area, string role = "", UserStatus status = UserStatus.Active)
    {
        Users.Add(new UserSummaryDto(id, $"Usuario {area}", $"{id:N}@bioflex.mx", role, area, status));
        return this;
    }
}

/// <summary>Cotizaciones de SISPRO en memoria: por defecto "Cotizada", sin capas (nada que comparar).</summary>
public sealed class FakeQuotationProvider : IQuotationProvider
{
    public Dictionary<string, QuotationSnapshot> Quotes { get; } = [];

    public FakeQuotationProvider With(string numero, string estado = "Cotizada", IReadOnlyList<QuotationLayer>? capas = null)
    {
        Quotes[numero] = new QuotationSnapshot(numero, estado, $"REF-{numero}", null, "Millar", new Dictionary<string, string>(), [],
            capas ?? [], null, 0, 0, [new QuotationQuantity(50, 2742.33m, 137116.16m)], []);
        return this;
    }

    public Task<QuotationSnapshot?> FindAsync(string numeroCotizacion, CancellationToken cancellationToken) =>
        Task.FromResult(Quotes.GetValueOrDefault(numeroCotizacion));
}
