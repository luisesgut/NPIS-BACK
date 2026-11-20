using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NpiManagement.Application.Users;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfCommercialTeamStore(NpiManagementDbContext dbContext, IMemoryCache cache) : ICommercialTeamStore
{
    // Se consulta en cada petición de un vendedor (claims): un minuto de caché, y al cambiar se invalida.
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);
    private static string Key(Guid userId) => $"commercial-director:{userId:N}";

    public async Task<Guid?> GetDirectorIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key(userId), out Guid? cached)) return cached;
        var directorId = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DirectorUserId)
            .FirstOrDefaultAsync(cancellationToken);
        cache.Set(Key(userId), directorId, CacheFor);
        return directorId;
    }

    public async Task<IReadOnlyList<TeamPersonDto>> ListMembersAsync(Guid directorUserId, CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking()
            .Where(u => u.DirectorUserId == directorUserId && u.Status == UserStatus.Active)
            .OrderBy(u => u.FullName)
            .Select(u => new TeamPersonDto(u.Id, u.FullName))
            .ToListAsync(cancellationToken);

    public async Task<MyTeamDto> GetMyTeamAsync(Guid userId, CancellationToken cancellationToken)
    {
        var directorId = await GetDirectorIdAsync(userId, cancellationToken);
        var director = directorId is { } id
            ? await dbContext.Users.AsNoTracking().Where(u => u.Id == id && u.Status == UserStatus.Active)
                .Select(u => new TeamPersonDto(u.Id, u.FullName)).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new MyTeamDto(director, await ListMembersAsync(userId, cancellationToken));
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> ListDirectorsAsync(CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking()
            .Where(u => u.DirectorUserId != null)
            .ToDictionaryAsync(u => u.Id, u => u.DirectorUserId!.Value, cancellationToken);

    public async Task SetDirectorAsync(Guid userId, Guid? directorUserId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        user.DirectorUserId = directorUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(Key(userId));
    }
}
