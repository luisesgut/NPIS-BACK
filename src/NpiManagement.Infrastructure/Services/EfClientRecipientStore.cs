using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.ClientRecipients;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfClientRecipientStore(NpiManagementDbContext dbContext, IClock clock) : IClientRecipientStore
{
    public async Task<IReadOnlyList<ClientRecipientDto>> ListAsync(Guid userId, string cliente, CancellationToken cancellationToken) =>
        await dbContext.Set<ClientRecipient>().AsNoTracking()
            .Where(r => r.UserId == userId && r.Cliente == cliente)
            .OrderBy(r => r.Kind == "To" ? 0 : 1).ThenBy(r => r.Email)
            .Select(r => new ClientRecipientDto(r.Email, r.Kind, r.Selected))
            .ToListAsync(cancellationToken);

    public async Task ReplaceAsync(Guid userId, string cliente, IReadOnlyList<ClientRecipientDto> recipients, CancellationToken cancellationToken)
    {
        var set = dbContext.Set<ClientRecipient>();
        var existing = await set.Where(r => r.UserId == userId && r.Cliente == cliente).ToListAsync(cancellationToken);
        var now = clock.UtcNow;

        foreach (var old in existing.Where(e => recipients.All(r => r.Email != e.Email)))
            set.Remove(old);

        foreach (var r in recipients)
        {
            var row = existing.FirstOrDefault(e => e.Email == r.Email);
            if (row is null)
            {
                set.Add(new ClientRecipient { Id = Guid.NewGuid(), UserId = userId, Cliente = cliente, Email = r.Email, Kind = r.Kind, Selected = r.Selected, UpdatedAt = now });
            }
            else if (row.Kind != r.Kind || row.Selected != r.Selected)
            {
                row.Kind = r.Kind;
                row.Selected = r.Selected;
                row.UpdatedAt = now;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
