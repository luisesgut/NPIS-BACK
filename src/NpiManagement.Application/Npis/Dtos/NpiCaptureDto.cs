using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Application.Npis.Dtos;

/// <summary>
/// Espejo de NpiCaptureData para el límite HTTP — mismos ~68 campos de captura (ver
/// docs/api-contract.md §3.1). Se mantiene separado del VO de dominio a propósito: Application
/// no expone tipos de Domain directamente en los contratos de entrada/salida.
/// </summary>
public sealed record NpiCaptureDto
{
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
    public string DesarrolloManga { get; init; } = string.Empty;
    /// <summary>Prensas donde hay manga de ese desarrollo (las que propuso el buscador de mangas), p. ej. "BOBST 1, VISION 1/2".</summary>
    public string PrensaManga { get; init; } = string.Empty;
    public string TipoEmpaque { get; init; } = string.Empty;

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

    public string FiguraEmbob { get; init; } = string.Empty;
    public string DiametroBobina { get; init; } = string.Empty;
    public string PesoBobina { get; init; } = string.Empty;
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
    public string TipoCaja { get; init; } = string.Empty;
    public string Ligas { get; init; } = "NO";
    public string CajaBultoTarima { get; init; } = string.Empty;
    public string DimTarimaBolsa { get; init; } = string.Empty;
    public string AlturaTarimaMaxBolsa { get; init; } = string.Empty;
    public string NotasEmpaqueEmbalaje { get; init; } = string.Empty;

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

    public NpiCaptureData ToDomain() => new()
    {
        NumeroCotizacion = NumeroCotizacion, Cliente = Cliente, TipoProducto = TipoProducto,
        Descripcion = Descripcion, DescripcionProducto = DescripcionProducto, NombreExtranjeroProducto = NombreExtranjeroProducto, CodigoProdCliente = CodigoProdCliente,
        ContactoCompras = ContactoCompras, ContactoClienteDiseno = ContactoClienteDiseno, EjecutivoVenta = EjecutivoVenta,
        Formato = Formato, Acabado = Acabado, Mercado = Mercado, SubMercado = SubMercado, UnidadVenta = UnidadVenta, TipoEntrada = TipoEntrada,
        CategoriaSAP = CategoriaSAP, TipoImpresion = TipoImpresion, TipoTinta = TipoTinta, DesarrolloManga = DesarrolloManga, PrensaManga = PrensaManga, TipoEmpaque = TipoEmpaque,
        UnidadMedidas = UnidadMedidas, UnidadCalibre = UnidadCalibre,
        Ancho = Ancho, Alto = Alto, FuelleExtendido = FuelleExtendido, SolapaPestana = SolapaPestana, UbicacionSolapa = UbicacionSolapa, Labio = Labio, Fuelles = Fuelles,
        PiTipoPelicula = PiTipoPelicula, TerminadoPI = TerminadoPI, PiCalibre = PiCalibre, FormulaLDPE_PI = FormulaLDPE_PI,
        PplTipoPelicula = PplTipoPelicula, TerminadoPPL = TerminadoPPL, PplCalibre = PplCalibre, FormulaLDPE_PPL = FormulaLDPE_PPL,
        PslTipoPelicula = PslTipoPelicula, TerminadoPSL = TerminadoPSL, PslCalibre = PslCalibre, FormulaLDPE_PSL = FormulaLDPE_PSL,
        Colgadera = Colgadera, Cierre = Cierre, OtrosElementos = OtrosElementos, Sello = Sello,
        Perforaciones = Perforaciones, NumeroPerforaciones = NumeroPerforaciones, DiametroPerforaciones = DiametroPerforaciones,
        Muesca = Muesca, Precorte = Precorte, PrecorteZipper = PrecorteZipper, PrecorteWicket = PrecorteWicket, PatronPerforado = PatronPerforado, ParcheRefuerzo = ParcheRefuerzo,
        Suaje = Suaje, AplicaPieDeCasa = AplicaPieDeCasa,
        ColgaderaDimensiones = ColgaderaDimensiones, MuescaDimensiones = MuescaDimensiones, PrecorteDimensiones = PrecorteDimensiones, PatronPerforadoDimensiones = PatronPerforadoDimensiones, ParcheRefuerzoDimensiones = ParcheRefuerzoDimensiones, SuajeDimensiones = SuajeDimensiones, CierreDimensiones = CierreDimensiones, OtrosElementosDimensiones = OtrosElementosDimensiones,
        FiguraEmbob = FiguraEmbob, DiametroBobina = DiametroBobina, PesoBobina = PesoBobina, Repeticion = Repeticion,
        BobinasPorTarima = BobinasPorTarima, AlturaTarimaMaxBobina = AlturaTarimaMaxBobina, MaxUniones = MaxUniones, ColorCintaUniones = ColorCintaUniones,
        Core = Core, TaponCore = TaponCore, DestareCore = DestareCore, DimTarimaBobina = DimTarimaBobina, PiezasCajaBulto = PiezasCajaBulto, TipoCaja = TipoCaja,
        Ligas = Ligas, CajaBultoTarima = CajaBultoTarima, DimTarimaBolsa = DimTarimaBolsa, AlturaTarimaMaxBolsa = AlturaTarimaMaxBolsa,
        NotasEmpaqueEmbalaje = NotasEmpaqueEmbalaje,
        BasePantone = BasePantone, BaseMuestra = BaseMuestra, VisitaCliente = VisitaCliente, PruebaColor = PruebaColor,
        ArchivosEditables = ArchivosEditables, MuestrasFisicas = MuestrasFisicas, Especificaciones = Especificaciones,
        Otros = Otros, CostoPreprensa = CostoPreprensa, Observaciones = Observaciones,
    };

    public static NpiCaptureDto FromDomain(NpiCaptureData c) => new()
    {
        NumeroCotizacion = c.NumeroCotizacion, Cliente = c.Cliente, TipoProducto = c.TipoProducto,
        Descripcion = c.Descripcion, DescripcionProducto = c.DescripcionProducto, NombreExtranjeroProducto = c.NombreExtranjeroProducto, CodigoProdCliente = c.CodigoProdCliente,
        ContactoCompras = c.ContactoCompras, ContactoClienteDiseno = c.ContactoClienteDiseno, EjecutivoVenta = c.EjecutivoVenta,
        Formato = c.Formato, Acabado = c.Acabado, Mercado = c.Mercado, SubMercado = c.SubMercado, UnidadVenta = c.UnidadVenta, TipoEntrada = c.TipoEntrada,
        CategoriaSAP = c.CategoriaSAP, TipoImpresion = c.TipoImpresion, TipoTinta = c.TipoTinta, DesarrolloManga = c.DesarrolloManga, PrensaManga = c.PrensaManga, TipoEmpaque = c.TipoEmpaque,
        UnidadMedidas = c.UnidadMedidas, UnidadCalibre = c.UnidadCalibre,
        Ancho = c.Ancho, Alto = c.Alto, FuelleExtendido = c.FuelleExtendido, SolapaPestana = c.SolapaPestana, UbicacionSolapa = c.UbicacionSolapa, Labio = c.Labio, Fuelles = c.Fuelles,
        PiTipoPelicula = c.PiTipoPelicula, TerminadoPI = c.TerminadoPI, PiCalibre = c.PiCalibre, FormulaLDPE_PI = c.FormulaLDPE_PI,
        PplTipoPelicula = c.PplTipoPelicula, TerminadoPPL = c.TerminadoPPL, PplCalibre = c.PplCalibre, FormulaLDPE_PPL = c.FormulaLDPE_PPL,
        PslTipoPelicula = c.PslTipoPelicula, TerminadoPSL = c.TerminadoPSL, PslCalibre = c.PslCalibre, FormulaLDPE_PSL = c.FormulaLDPE_PSL,
        Colgadera = c.Colgadera, Cierre = c.Cierre, OtrosElementos = c.OtrosElementos, Sello = c.Sello,
        Perforaciones = c.Perforaciones, NumeroPerforaciones = c.NumeroPerforaciones, DiametroPerforaciones = c.DiametroPerforaciones,
        Muesca = c.Muesca, Precorte = c.Precorte, PrecorteZipper = c.PrecorteZipper, PrecorteWicket = c.PrecorteWicket, PatronPerforado = c.PatronPerforado, ParcheRefuerzo = c.ParcheRefuerzo,
        Suaje = c.Suaje, AplicaPieDeCasa = c.AplicaPieDeCasa,
        ColgaderaDimensiones = c.ColgaderaDimensiones, MuescaDimensiones = c.MuescaDimensiones, PrecorteDimensiones = c.PrecorteDimensiones, PatronPerforadoDimensiones = c.PatronPerforadoDimensiones, ParcheRefuerzoDimensiones = c.ParcheRefuerzoDimensiones, SuajeDimensiones = c.SuajeDimensiones, CierreDimensiones = c.CierreDimensiones, OtrosElementosDimensiones = c.OtrosElementosDimensiones,
        FiguraEmbob = c.FiguraEmbob, DiametroBobina = c.DiametroBobina, PesoBobina = c.PesoBobina, Repeticion = c.Repeticion,
        BobinasPorTarima = c.BobinasPorTarima, AlturaTarimaMaxBobina = c.AlturaTarimaMaxBobina, MaxUniones = c.MaxUniones, ColorCintaUniones = c.ColorCintaUniones,
        Core = c.Core, TaponCore = c.TaponCore, DestareCore = c.DestareCore, DimTarimaBobina = c.DimTarimaBobina, PiezasCajaBulto = c.PiezasCajaBulto, TipoCaja = c.TipoCaja,
        Ligas = c.Ligas, CajaBultoTarima = c.CajaBultoTarima, DimTarimaBolsa = c.DimTarimaBolsa, AlturaTarimaMaxBolsa = c.AlturaTarimaMaxBolsa,
        NotasEmpaqueEmbalaje = c.NotasEmpaqueEmbalaje,
        BasePantone = c.BasePantone, BaseMuestra = c.BaseMuestra, VisitaCliente = c.VisitaCliente, PruebaColor = c.PruebaColor,
        ArchivosEditables = c.ArchivosEditables, MuestrasFisicas = c.MuestrasFisicas, Especificaciones = c.Especificaciones,
        Otros = c.Otros, CostoPreprensa = c.CostoPreprensa, Observaciones = c.Observaciones,
    };
}
