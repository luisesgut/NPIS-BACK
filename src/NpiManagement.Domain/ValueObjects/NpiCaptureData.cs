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
    /// <summary>
    /// Nombres que se guardan en MAYÚSCULAS (2026-11-09): cada asesor los escribía a su manera
    /// ("Mr Lucky" / "MR LUCKY"). Respeta acentos y Ñ. Observaciones y notas se quedan como se escriben.
    /// Al comparar capturas (CaptureDiff) estos campos ignoran mayúsculas: pasarlos a mayúsculas no es un cambio.
    /// </summary>
    public static readonly IReadOnlySet<string> UppercaseFields = new HashSet<string>
    {
        nameof(Cliente), nameof(Descripcion), nameof(DescripcionProducto), nameof(NombreExtranjeroProducto),
        nameof(CodigoProdCliente), nameof(ContactoCompras), nameof(ContactoClienteDiseno),
    };

    public NpiCaptureData WithUppercaseNames() => this with
    {
        Cliente = Cliente.ToUpperInvariant(),
        Descripcion = Descripcion.ToUpperInvariant(),
        DescripcionProducto = DescripcionProducto.ToUpperInvariant(),
        NombreExtranjeroProducto = NombreExtranjeroProducto.ToUpperInvariant(),
        CodigoProdCliente = CodigoProdCliente.ToUpperInvariant(),
        ContactoCompras = ContactoCompras.ToUpperInvariant(),
        ContactoClienteDiseno = ContactoClienteDiseno.ToUpperInvariant(),
    };

    // Información General (incluye snapshot de cotización — campos "verdes")
    public string NumeroCotizacion { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public string TipoProducto { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string DescripcionProducto { get; init; } = string.Empty;
    public string NombreExtranjeroProducto { get; init; } = string.Empty;
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
    /// <summary>Desarrollo (circunferencia, mm) de la manga de impresión flexo; opción del catálogo DESARROLLO MANGA.</summary>
    public string DesarrolloManga { get; init; } = string.Empty;
    /// <summary>Prensas donde hay manga de ese desarrollo (las que propuso el buscador de mangas), p. ej. "BOBST 1, VISION 1/2".</summary>
    public string PrensaManga { get; init; } = string.Empty;
    public string TipoEmpaque { get; init; } = string.Empty;

    // Producto
    /// <summary>Unidad en que el vendedor capturó las medidas (mm, cm, pulg). Los valores siempre se guardan convertidos a mm; vacío = mm.</summary>
    public string UnidadMedidas { get; init; } = string.Empty;
    /// <summary>Unidad en que se capturó el calibre (micras, galgas, mils). Los valores siempre se guardan convertidos a micras; vacío = micras.</summary>
    public string UnidadCalibre { get; init; } = string.Empty;
    public string Ancho { get; init; } = string.Empty;
    public string Alto { get; init; } = string.Empty;
    public string FuelleExtendido { get; init; } = string.Empty;
    public string SolapaPestana { get; init; } = string.Empty;
    /// <summary>Dónde va la solapa: "ARRIBA" o "ABAJO". Vacío = arriba (los NPIs anteriores a 2026-11-04).</summary>
    public string UbicacionSolapa { get; init; } = string.Empty;
    /// <summary>Labio (mm) de la bolsa wicket.</summary>
    public string Labio { get; init; } = string.Empty;
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
    public string PrecorteZipper { get; init; } = "NO";
    public string PrecorteWicket { get; init; } = "NO";
    public string PatronPerforado { get; init; } = "NO";
    public string ParcheRefuerzo { get; init; } = "NO";
    public string Suaje { get; init; } = "NO";
    /// <summary>Columna "DIMENSIONES (mm)" de cada elemento en el formato VTS-FOR-01 (texto libre, ej. "2 mm").</summary>
    public string ColgaderaDimensiones { get; init; } = string.Empty;
    public string MuescaDimensiones { get; init; } = string.Empty;
    public string PrecorteDimensiones { get; init; } = string.Empty;
    public string PatronPerforadoDimensiones { get; init; } = string.Empty;
    public string ParcheRefuerzoDimensiones { get; init; } = string.Empty;
    public string SuajeDimensiones { get; init; } = string.Empty;
    public string CierreDimensiones { get; init; } = string.Empty;
    public string OtrosElementosDimensiones { get; init; } = string.Empty;
    public string AplicaPieDeCasa { get; init; } = "NO";

    // Empaque y Embalaje
    public string FiguraEmbob { get; init; } = string.Empty;
    public string DiametroBobina { get; init; } = string.Empty;
    public string PesoBobina { get; init; } = string.Empty;
    /// <summary>Largo de una pieza (repetición) para calcular piezas por bobina; se guarda en mm como las demás medidas.</summary>
    public string Repeticion { get; init; } = string.Empty;
    public string BobinasPorTarima { get; init; } = string.Empty;
    public string AlturaTarimaMaxBobina { get; init; } = string.Empty;
    public string MaxUniones { get; init; } = string.Empty;
    /// <summary>Color de la cinta de las uniones de la bobina: BLANCA, ROJA o el que se escriba (opción "Otro").</summary>
    public string ColorCintaUniones { get; init; } = string.Empty;
    public string Core { get; init; } = string.Empty;
    public string TaponCore { get; init; } = "NO";
    public string DestareCore { get; init; } = "NO";
    public string DimTarimaBobina { get; init; } = string.Empty;
    public string PiezasCajaBulto { get; init; } = string.Empty;
    /// <summary>Tipo de caja del producto terminado (bolsa / pouch); opción del catálogo TIPO DE CAJA.</summary>
    public string TipoCaja { get; init; } = string.Empty;
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
