using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Services;

/// <summary>
/// Implementación local de IQuotationProvider sobre la tabla `Quotations` en SQL Server.
/// Reemplazable después por una integración externa sin tocar Application (ADR-018).
/// </summary>
public sealed class EfQuotationProvider(NpiManagementDbContext dbContext) : IQuotationProvider
{
    public async Task<QuotationSnapshot?> FindAsync(string numeroCotizacion, CancellationToken cancellationToken)
    {
        var q = await dbContext.Quotations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.NumeroCotizacion == numeroCotizacion, cancellationToken);

        return q is null
            ? null
            : new QuotationSnapshot(
                q.NumeroCotizacion, q.Cliente, q.TipoProducto, q.Descripcion, q.DescripcionProducto,
                q.EjecutivoVenta, q.Ancho, q.PiTipoPelicula, q.PiCalibre, q.PplTipoPelicula, q.PplCalibre,
                q.PslTipoPelicula, q.PslCalibre, q.PesoBobina, q.TaponCore, q.PiezasCajaBulto);
    }
}
