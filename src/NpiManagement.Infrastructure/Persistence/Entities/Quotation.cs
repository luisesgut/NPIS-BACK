namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>
/// Cotización pre-existente (ver README — se genera en "otra plataforma"). Entidad de ciclo de
/// vida independiente, sin invariantes de negocio propias; por eso vive directamente en
/// Infrastructure como POCO simple en vez de en Domain (ver docs/domain-model.md §8).
/// Respalda la implementación local de IQuotationProvider.
/// </summary>
public sealed class Quotation
{
    public Guid Id { get; set; }
    public string NumeroCotizacion { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string TipoProducto { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string DescripcionProducto { get; set; } = string.Empty;
    public string EjecutivoVenta { get; set; } = string.Empty;
    public string Ancho { get; set; } = string.Empty;
    public string PiTipoPelicula { get; set; } = string.Empty;
    public string PiCalibre { get; set; } = string.Empty;
    public string PplTipoPelicula { get; set; } = string.Empty;
    public string PplCalibre { get; set; } = string.Empty;
    public string PslTipoPelicula { get; set; } = string.Empty;
    public string PslCalibre { get; set; } = string.Empty;
    public string PesoBobina { get; set; } = string.Empty;
    public string TaponCore { get; set; } = string.Empty;
    public string PiezasCajaBulto { get; set; } = string.Empty;
}
