using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain.Entities;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfNpiChatStore(NpiManagementDbContext dbContext) : INpiChatStore
{
    public async Task<NpiChatTarget?> FindNpiAsync(Guid npiId, CancellationToken cancellationToken)
    {
        // Proyección: sólo folio y responsables, nunca el agregado con todas sus colecciones.
        var row = await dbContext.Npis
            .AsNoTracking()
            .Where(n => n.Id == npiId)
            .Select(n => new { n.Id, n.NpiNumber, n.CreatedByUserId, n.AssignedCommercialUserId, n.AssignedEngineeringUserId, n.AssignedDesignUserId })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new NpiChatTarget(row.Id, row.NpiNumber?.Value,
                new NpiOwnership(row.CreatedByUserId, row.AssignedCommercialUserId, row.AssignedEngineeringUserId, row.AssignedDesignUserId));
    }

    public void Add(NpiComment message) => dbContext.NpiComments.Add(message);

    public async Task<PagedResult<NpiComment>> ListAsync(Guid npiId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.NpiComments.AsNoTracking().Where(c => c.NpiId == npiId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NpiComment>(items, page, pageSize, total);
    }

    public async Task MarkReadAsync(Guid npiId, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        var read = dbContext.NpiChatReads.Local.FirstOrDefault(r => r.NpiId == npiId && r.UserId == userId)
            ?? await dbContext.NpiChatReads.FirstOrDefaultAsync(r => r.NpiId == npiId && r.UserId == userId, cancellationToken);

        if (read is null)
            dbContext.NpiChatReads.Add(new NpiChatRead { NpiId = npiId, UserId = userId, LastReadAt = readAt });
        else if (readAt > read.LastReadAt)
            read.LastReadAt = readAt;
    }

    public async Task<IReadOnlyList<ChatUnreadDto>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var myNpiIds = dbContext.Npis
            .Where(n => n.CreatedByUserId == userId || n.AssignedCommercialUserId == userId ||
                        n.AssignedEngineeringUserId == userId || n.AssignedDesignUserId == userId)
            .Select(n => n.Id);

        var unread = await dbContext.NpiComments
            .AsNoTracking()
            .Where(c => myNpiIds.Contains(c.NpiId) && c.AuthorUserId != userId)
            .Where(c => !dbContext.NpiChatReads.Any(r => r.NpiId == c.NpiId && r.UserId == userId && r.LastReadAt >= c.CreatedAt))
            .GroupBy(c => c.NpiId)
            .Select(g => new { NpiId = g.Key, Count = g.Count(), Last = g.Max(c => c.CreatedAt) })
            .ToListAsync(cancellationToken);

        if (unread.Count == 0) return [];

        var ids = unread.Select(u => u.NpiId).ToList();
        var numbers = await dbContext.Npis
            .AsNoTracking()
            .Where(n => ids.Contains(n.Id))
            .Select(n => new { n.Id, n.NpiNumber })
            .ToDictionaryAsync(n => n.Id, n => n.NpiNumber?.Value, cancellationToken);

        return unread
            .OrderByDescending(u => u.Last)
            .Select(u => new ChatUnreadDto(u.NpiId, numbers.GetValueOrDefault(u.NpiId), u.Count, u.Last))
            .ToList();
    }
}
