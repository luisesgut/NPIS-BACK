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
        Descripcion = Descripcion, DescripcionProducto = DescripcionProducto, CodigoProdCliente = CodigoProdCliente,
        ContactoCompras = ContactoCompras, ContactoClienteDiseno = ContactoClienteDiseno, EjecutivoVenta = EjecutivoVenta,
        Formato = Formato, Acabado = Acabado, Mercado = Mercado, SubMercado = SubMercado, UnidadVenta = UnidadVenta, TipoEntrada = TipoEntrada,
        CategoriaSAP = CategoriaSAP, TipoImpresion = TipoImpresion, TipoTinta = TipoTinta, TipoEmpaque = TipoEmpaque,
        Ancho = Ancho, Alto = Alto, FuelleExtendido = FuelleExtendido, SolapaPestana = SolapaPestana, Fuelles = Fuelles,
        PiTipoPelicula = PiTipoPelicula, TerminadoPI = TerminadoPI, PiCalibre = PiCalibre, FormulaLDPE_PI = FormulaLDPE_PI,
        PplTipoPelicula = PplTipoPelicula, TerminadoPPL = TerminadoPPL, PplCalibre = PplCalibre, FormulaLDPE_PPL = FormulaLDPE_PPL,
        PslTipoPelicula = PslTipoPelicula, TerminadoPSL = TerminadoPSL, PslCalibre = PslCalibre, FormulaLDPE_PSL = FormulaLDPE_PSL,
        Colgadera = Colgadera, Cierre = Cierre, OtrosElementos = OtrosElementos, Sello = Sello,
        Perforaciones = Perforaciones, NumeroPerforaciones = NumeroPerforaciones, DiametroPerforaciones = DiametroPerforaciones,
        Muesca = Muesca, Precorte = Precorte, PatronPerforado = PatronPerforado, ParcheRefuerzo = ParcheRefuerzo,
        Suaje = Suaje, AplicaPieDeCasa = AplicaPieDeCasa,
        FiguraEmbob = FiguraEmbob, DiametroBobina = DiametroBobina, PesoBobina = PesoBobina,
        BobinasPorTarima = BobinasPorTarima, AlturaTarimaMaxBobina = AlturaTarimaMaxBobina, MaxUniones = MaxUniones,
        Core = Core, TaponCore = TaponCore, DestareCore = DestareCore, DimTarimaBobina = DimTarimaBobina, PiezasCajaBulto = PiezasCajaBulto,
        Ligas = Ligas, CajaBultoTarima = CajaBultoTarima, DimTarimaBolsa = DimTarimaBolsa, AlturaTarimaMaxBolsa = AlturaTarimaMaxBolsa,
        NotasEmpaqueEmbalaje = NotasEmpaqueEmbalaje,
        BasePantone = BasePantone, BaseMuestra = BaseMuestra, VisitaCliente = VisitaCliente, PruebaColor = PruebaColor,
        ArchivosEditables = ArchivosEditables, MuestrasFisicas = MuestrasFisicas, Especificaciones = Especificaciones,
        Otros = Otros, CostoPreprensa = CostoPreprensa, Observaciones = Observaciones,
    };

    public static NpiCaptureDto FromDomain(NpiCaptureData c) => new()
    {
        NumeroCotizacion = c.NumeroCotizacion, Cliente = c.Cliente, TipoProducto = c.TipoProducto,
        Descripcion = c.Descripcion, DescripcionProducto = c.DescripcionProducto, CodigoProdCliente = c.CodigoProdCliente,
        ContactoCompras = c.ContactoCompras, ContactoClienteDiseno = c.ContactoClienteDiseno, EjecutivoVenta = c.EjecutivoVenta,
        Formato = c.Formato, Acabado = c.Acabado, Mercado = c.Mercado, SubMercado = c.SubMercado, UnidadVenta = c.UnidadVenta, TipoEntrada = c.TipoEntrada,
        CategoriaSAP = c.CategoriaSAP, TipoImpresion = c.TipoImpresion, TipoTinta = c.TipoTinta, TipoEmpaque = c.TipoEmpaque,
        Ancho = c.Ancho, Alto = c.Alto, FuelleExtendido = c.FuelleExtendido, SolapaPestana = c.SolapaPestana, Fuelles = c.Fuelles,
        PiTipoPelicula = c.PiTipoPelicula, TerminadoPI = c.TerminadoPI, PiCalibre = c.PiCalibre, FormulaLDPE_PI = c.FormulaLDPE_PI,
        PplTipoPelicula = c.PplTipoPelicula, TerminadoPPL = c.TerminadoPPL, PplCalibre = c.PplCalibre, FormulaLDPE_PPL = c.FormulaLDPE_PPL,
        PslTipoPelicula = c.PslTipoPelicula, TerminadoPSL = c.TerminadoPSL, PslCalibre = c.PslCalibre, FormulaLDPE_PSL = c.FormulaLDPE_PSL,
        Colgadera = c.Colgadera, Cierre = c.Cierre, OtrosElementos = c.OtrosElementos, Sello = c.Sello,
        Perforaciones = c.Perforaciones, NumeroPerforaciones = c.NumeroPerforaciones, DiametroPerforaciones = c.DiametroPerforaciones,
        Muesca = c.Muesca, Precorte = c.Precorte, PatronPerforado = c.PatronPerforado, ParcheRefuerzo = c.ParcheRefuerzo,
        Suaje = c.Suaje, AplicaPieDeCasa = c.AplicaPieDeCasa,
        FiguraEmbob = c.FiguraEmbob, DiametroBobina = c.DiametroBobina, PesoBobina = c.PesoBobina,
        BobinasPorTarima = c.BobinasPorTarima, AlturaTarimaMaxBobina = c.AlturaTarimaMaxBobina, MaxUniones = c.MaxUniones,
        Core = c.Core, TaponCore = c.TaponCore, DestareCore = c.DestareCore, DimTarimaBobina = c.DimTarimaBobina, PiezasCajaBulto = c.PiezasCajaBulto,
        Ligas = c.Ligas, CajaBultoTarima = c.CajaBultoTarima, DimTarimaBolsa = c.DimTarimaBolsa, AlturaTarimaMaxBolsa = c.AlturaTarimaMaxBolsa,
        NotasEmpaqueEmbalaje = c.NotasEmpaqueEmbalaje,
        BasePantone = c.BasePantone, BaseMuestra = c.BaseMuestra, VisitaCliente = c.VisitaCliente, PruebaColor = c.PruebaColor,
        ArchivosEditables = c.ArchivosEditables, MuestrasFisicas = c.MuestrasFisicas, Especificaciones = c.Especificaciones,
        Otros = c.Otros, CostoPreprensa = c.CostoPreprensa, Observaciones = c.Observaciones,
    };
}
