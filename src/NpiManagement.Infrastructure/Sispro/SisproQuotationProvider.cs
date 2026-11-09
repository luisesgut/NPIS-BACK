using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Sispro;

/// <summary>
/// IQuotationProvider sobre SISPRO (reemplaza la tabla local Quotations, ADR-018): lee la cotización,
/// carga las equivalencias de Ingeniería y los catálogos vigentes, y delega la traducción en
/// SisproQuotationMapper.
/// </summary>
public sealed class SisproQuotationProvider(ISisproClient client, NpiManagementDbContext dbContext, ICatalogReader catalogReader) : IQuotationProvider
{
    public async Task<QuotationSnapshot?> FindAsync(string numeroCotizacion, CancellationToken cancellationToken)
    {
        var dto = await client.GetCotizacionAsync(numeroCotizacion, cancellationToken);
        if (dto is null) return null;

        var equivalences = (await dbContext.SisproMaterialEquivalences.AsNoTracking().ToListAsync(cancellationToken))
            .GroupBy(e => SisproQuotationMapper.Normalize(e.MaterialSispro))
            .ToDictionary(g => g.Key, g => new SisproEquivalence(g.First().TipoPelicula, g.First().Terminado, g.First().FormulaLdpe));

        var catalogs = (await catalogReader.ListAsync(cancellationToken))
            .ToDictionary(c => c.Key, c => (IReadOnlyList<string>)c.Options.Where(o => o.IsActive).Select(o => o.Value).ToList());

        // Los nombres de los usuarios sirven para no confundir al vendedor con el cliente en la referencia.
        var people = await dbContext.Users.AsNoTracking().Select(u => u.FullName).Where(n => n != "").ToListAsync(cancellationToken);

        return SisproQuotationMapper.Map(dto, equivalences, catalogs, people);
    }
}
