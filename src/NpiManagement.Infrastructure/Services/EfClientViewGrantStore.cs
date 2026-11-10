using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NpiManagement.Application.Users;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfClientViewGrantStore(NpiManagementDbContext dbContext, IMemoryCache cache, IClock clock) : IClientViewGrantStore
{
    // Se consulta en cada petición autenticada (ClientViewClaimsTransformation): un minuto de caché
    // basta, y al cambiar los permisos se invalida en el momento.
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);
    private static string Key(Guid userId) => $"client-views:{userId:N}";

    public async Task<ClientGrants> GetGrantsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key(userId), out ClientGrants? cached) && cached is not null) return cached;
        var rows = await dbContext.UserClientViews.AsNoTracking()
            .Where(v => v.UserId == userId)
            .Select(v => new { v.Cliente, v.CanWork })
            .ToListAsync(cancellationToken);
        var grants = new ClientGrants(
            rows.Select(r => r.Cliente).ToHashSet(StringComparer.OrdinalIgnoreCase),
            rows.Where(r => r.CanWork).Select(r => r.Cliente).ToHashSet(StringComparer.OrdinalIgnoreCase));
        cache.Set(Key(userId), grants, CacheFor);
        return grants;
    }

    public async Task<IReadOnlyList<ClientViewGrantDto>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.UserClientViews.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderBy(v => v.Cliente)
            .Select(v => new ClientViewGrantDto(v.Cliente, v.CanWork))
            .ToListAsync(cancellationToken);

    public async Task ReplaceAsync(Guid userId, IReadOnlyList<ClientViewGrantDto> grants, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UserClientViews.Where(v => v.UserId == userId).ToListAsync(cancellationToken);
        var wanted = grants.ToDictionary(g => g.Cliente, g => g.CanWork, StringComparer.OrdinalIgnoreCase);
        dbContext.UserClientViews.RemoveRange(existing.Where(v => !wanted.ContainsKey(v.Cliente)));
        var now = clock.UtcNow;
        foreach (var row in existing.Where(v => wanted.ContainsKey(v.Cliente)))
            row.CanWork = wanted[row.Cliente];
        var kept = existing.Select(v => v.Cliente).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var grant in grants.Where(g => !kept.Contains(g.Cliente)))
            dbContext.UserClientViews.Add(new UserClientView { UserId = userId, Cliente = grant.Cliente, CanWork = grant.CanWork, GrantedByUserId = changedByUserId, GrantedAt = now });
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(Key(userId));
    }

    public async Task<IReadOnlyList<Guid>> ListWorkersAsync(string cliente, CancellationToken cancellationToken)
    {
        var normalized = ClientViewGrants.Normalize(cliente);
        return await dbContext.UserClientViews.AsNoTracking()
            .Where(v => v.Cliente == normalized && v.CanWork)
            .Select(v => v.UserId)
            .ToListAsync(cancellationToken);
    }
}
