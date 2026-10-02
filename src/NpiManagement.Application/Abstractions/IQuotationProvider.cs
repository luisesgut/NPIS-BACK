namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Snapshot de los campos "verdes" de una cotización ya existente (ver README — se capturan en
/// otra plataforma). Desacoplado a propósito de la entidad EF de Infrastructure.
/// </summary>
public sealed record QuotationSnapshot(
    string NumeroCotizacion,
    string Cliente,
    string TipoProducto,
    string Descripcion,
    string DescripcionProducto,
    string EjecutivoVenta,
    string Ancho,
    string PiTipoPelicula,
    string PiCalibre,
    string PplTipoPelicula,
    string PplCalibre,
    string PslTipoPelicula,
    string PslCalibre,
    string PesoBobina,
    string TaponCore,
    string PiezasCajaBulto);

/// <summary>
/// Proveedor de cotizaciones. La implementación inicial (Infrastructure) consulta la tabla local
/// `Quotation` en SQL Server; se puede reemplazar después por una integración externa sin tocar
/// Application (ver docs/architecture-decisions.md ADR-018 y docs/domain-model.md §8).
/// </summary>
public interface IQuotationProvider
{
    Task<QuotationSnapshot?> FindAsync(string numeroCotizacion, CancellationToken cancellationToken);
}
