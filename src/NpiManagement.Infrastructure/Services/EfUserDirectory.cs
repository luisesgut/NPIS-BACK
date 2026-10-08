using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Services;

/// <summary>Usuarios consultables por área, para asignaciones (api-contract.md §2).</summary>
public sealed class EfUserDirectory(NpiManagementDbContext dbContext, UserManager<ApplicationUser> userManager) : IUserDirectory
{
    public async Task<IReadOnlyList<UserSummaryDto>> ListAsync(AreaType? area, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().AsQueryable();
        if (area is { } value)
            query = query.Where(u => u.Area == value);

        var users = await query.OrderBy(u => u.FullName).ToListAsync(cancellationToken);

        var result = new List<UserSummaryDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserSummaryDto(user.Id, user.FullName, user.Email ?? string.Empty, roles.FirstOrDefault() ?? string.Empty, user.Area, user.Status));
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, string>();

        var names = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return names;
    }

    public async Task<UserSummaryDto?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;

        var roles = await userManager.GetRolesAsync(user);
        return new UserSummaryDto(user.Id, user.FullName, user.Email ?? string.Empty, roles.FirstOrDefault() ?? string.Empty, user.Area, user.Status);
    }
}
