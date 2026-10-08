using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain.Entities;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfNpiCaptureHistoryStore(NpiManagementDbContext dbContext) : INpiCaptureHistoryStore
{
    public void AddRange(IEnumerable<CaptureChange> changes) => dbContext.NpiCaptureChanges.AddRange(changes);

    public async Task<IReadOnlyList<CaptureChange>> ListAsync(Guid npiId, CancellationToken cancellationToken) =>
        await dbContext.NpiCaptureChanges
            .AsNoTracking()
            .Where(c => c.NpiId == npiId)
            .OrderBy(c => c.ChangedAt)
            .ToListAsync(cancellationToken);
}
