using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfCatalogReader(NpiManagementDbContext dbContext) : ICatalogReader
{
    public async Task<IReadOnlyList<CatalogDto>> ListAsync(CancellationToken cancellationToken)
    {
        var catalogs = await dbContext.Catalogs
            .AsNoTracking()
            .Include(c => c.Options)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);

        return catalogs
            .Select(c => new CatalogDto(
                c.Key,
                c.DisplayName,
                c.Options.OrderBy(o => o.DisplayOrder).Select(o => new CatalogOptionDto(o.Value, o.IsActive)).ToList()))
            .ToList();
    }
}
