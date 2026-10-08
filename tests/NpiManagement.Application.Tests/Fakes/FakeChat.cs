using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Application.Tests.Fakes;

/// <summary>Chat en memoria. Los NPIs se registran desde el agregado para reutilizar sus asignaciones reales.</summary>
public sealed class FakeNpiChatStore : INpiChatStore
{
    private readonly Dictionary<Guid, Npi> _npis = [];

    public List<NpiComment> Messages { get; } = [];
    public Dictionary<(Guid NpiId, Guid UserId), DateTimeOffset> Reads { get; } = [];

    public FakeNpiChatStore With(Npi npi)
    {
        _npis[npi.Id] = npi;
        return this;
    }

    public Task<NpiChatTarget?> FindNpiAsync(Guid npiId, CancellationToken cancellationToken) =>
        Task.FromResult(_npis.TryGetValue(npiId, out var n)
            ? new NpiChatTarget(n.Id, n.NpiNumber?.Value, new NpiOwnership(n.CreatedByUserId, n.AssignedCommercialUserId, n.AssignedEngineeringUserId, n.AssignedDesignUserId))
            : null);

    public void Add(NpiComment message) => Messages.Add(message);

    public Task<PagedResult<NpiComment>> ListAsync(Guid npiId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = Messages.Where(m => m.NpiId == npiId).OrderByDescending(m => m.CreatedAt).ToList();
        return Task.FromResult(new PagedResult<NpiComment>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count));
    }

    public Task MarkReadAsync(Guid npiId, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        if (!Reads.TryGetValue((npiId, userId), out var current) || readAt > current)
            Reads[(npiId, userId)] = readAt;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatUnreadDto>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ChatUnreadDto>>([]);
}

public sealed class FakeChatRealtime : IChatRealtime
{
    public List<(CommentDto Message, IReadOnlyCollection<Guid> Notified)> Posted { get; } = [];

    public Task MessagePostedAsync(CommentDto message, IReadOnlyCollection<Guid> notifiedUserIds, CancellationToken cancellationToken)
    {
        Posted.Add((message, notifiedUserIds));
        return Task.CompletedTask;
    }
}
