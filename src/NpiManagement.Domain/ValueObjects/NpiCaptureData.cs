namespace NpiManagement.Domain.ValueObjects;

/// <summary>
/// Snapshot inmutable de todos los campos de captura del NPI (verde/amarillo/azul/naranja,
/// catalogados en el frontend en src/data/npiFields.ts + los campos "verdes" de cotización).
/// No tienen ciclo de vida, autorización ni versión propios — permanecen como columnas
/// escalares del agregado Npi (ver docs/domain-model.md §2.2). Sin comportamiento de negocio:
/// es un contenedor de datos descriptivos, reemplazado en bloque en cada edición de borrador.
/// </summary>
public sealed record NpiCaptureData
{
    // Información General (incluye snapshot de cotización — campos "verdes")
    public string NumeroCotizacion { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public string TipoProducto { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string DescripcionProducto { get; init; } = string.Empty;
    public string CodigoProdCliente { get; init; } = string.Empty;
    public string ContactoCompras { get; init; } = string.Empty;
    public string ContactoClienteDiseno { get; init; } = string.Empty;
    public string EjecutivoVenta { get; init; } = string.Empty;
    public string Formato { get; init; } = string.Empty;
    public string Acabado { get; init; } = string.Empty;
    public string Mercado { get; init; } = string.Empty;
    public string SubMercado { get; init; } = string.Empty;
    public string UnidadVenta { get; init; } = string.Empty;
    public string TipoEntrada { get; init; } = string.Empty;
    public string CategoriaSAP { get; init; } = string.Empty;
    public string TipoImpresion { get; init; } = string.Empty;
    public string TipoTinta { get; init; } = string.Empty;
    public string TipoEmpaque { get; init; } = string.Empty;

    // Producto
    public string Ancho { get; init; } = string.Empty;
    public string Alto { get; init; } = string.Empty;
    public string FuelleExtendido { get; init; } = string.Empty;
    public string SolapaPestana { get; init; } = string.Empty;
    public string Fuelles { get; init; } = string.Empty;
    public string PiTipoPelicula { get; init; } = string.Empty;
    public string TerminadoPI { get; init; } = string.Empty;
    public string PiCalibre { get; init; } = string.Empty;
    public string FormulaLDPE_PI { get; init; } = string.Empty;
    public string PplTipoPelicula { get; init; } = string.Empty;
    public string TerminadoPPL { get; init; } = string.Empty;
    public string PplCalibre { get; init; } = string.Empty;
    public string FormulaLDPE_PPL { get; init; } = string.Empty;
    public string PslTipoPelicula { get; init; } = string.Empty;
    public string TerminadoPSL { get; init; } = string.Empty;
    public string PslCalibre { get; init; } = string.Empty;
    public string FormulaLDPE_PSL { get; init; } = string.Empty;

    // Elementos y Configuración (SI/NO como texto, igual que el frontend)
    public string Colgadera { get; init; } = string.Empty;
    public string Cierre { get; init; } = string.Empty;
    public string OtrosElementos { get; init; } = string.Empty;
    public string Sello { get; init; } = string.Empty;
    public string Perforaciones { get; init; } = string.Empty;
    public string NumeroPerforaciones { get; init; } = string.Empty;
    public string DiametroPerforaciones { get; init; } = string.Empty;
    public string Muesca { get; init; } = "NO";
    public string Precorte { get; init; } = "NO";
    public string PatronPerforado { get; init; } = "NO";
    public string ParcheRefuerzo { get; init; } = "NO";
    public string Suaje { get; init; } = "NO";
    public string AplicaPieDeCasa { get; init; } = "NO";

    // Empaque y Embalaje
    public string FiguraEmbob { get; init; } = string.Empty;
    public string DiametroBobina { get; init; } = string.Empty;
    public string PesoBobina { get; init; } = string.Empty;
    public string BobinasPorTarima { get; init; } = string.Empty;
    public string AlturaTarimaMaxBobina { get; init; } = string.Empty;
    public string MaxUniones { get; init; } = string.Empty;
    public string Core { get; init; } = string.Empty;
    public string TaponCore { get; init; } = "NO";
    public string DestareCore { get; init; } = "NO";
    public string DimTarimaBobina { get; init; } = string.Empty;
    public string PiezasCajaBulto { get; init; } = string.Empty;
    public string Ligas { get; init; } = "NO";
    public string CajaBultoTarima { get; init; } = string.Empty;
    public string DimTarimaBolsa { get; init; } = string.Empty;
    public string AlturaTarimaMaxBolsa { get; init; } = string.Empty;
    public string NotasEmpaqueEmbalaje { get; init; } = string.Empty;

    // Información Complementaria
    public string BasePantone { get; init; } = "NO";
    public string BaseMuestra { get; init; } = "NO";
    public string VisitaCliente { get; init; } = "NO";
    public string PruebaColor { get; init; } = "NO";
    public string ArchivosEditables { get; init; } = "NO";
    public string MuestrasFisicas { get; init; } = "NO";
    public string Especificaciones { get; init; } = "NO";
    public string Otros { get; init; } = "NO";
    public string CostoPreprensa { get; init; } = string.Empty;
    public string Observaciones { get; init; } = string.Empty;

    public static readonly NpiCaptureData Empty = new();
}
