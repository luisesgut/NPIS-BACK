using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Clients;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Nombres de cliente directo en SQL: el listado es un GROUP BY sobre Capture_Cliente y unificar es
/// un UPDATE de esa sola columna — sin cargar los agregados Npi (no es una acción del flujo ni deja
/// bitácora; ver MergeClientNamesCommandHandler).
/// </summary>
public sealed class EfClientNameStore(NpiManagementDbContext dbContext) : IClientNameStore
{
    public async Task<IReadOnlyList<ClientNameDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Npis.AsNoTracking()
            .Where(n => n.Capture.Cliente != "")
            .GroupBy(n => n.Capture.Cliente)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ClientNameDto(r.Name, r.Count))
            .ToList();
    }

    public async Task<int> RenameAsync(IReadOnlyCollection<string> from, string to, CancellationToken cancellationToken)
    {
        var changed = 0;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var name in from)
        {
            // Parámetros interpolados (no concatenados): sin riesgo de inyección. RowVersion se
            // actualiza solo, así que quien tenga el NPI abierto recibe el aviso de concurrencia normal.
            changed += await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Npis SET Capture_Cliente = {to} WHERE Capture_Cliente = {name}", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }
}
