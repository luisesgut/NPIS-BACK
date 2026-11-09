namespace NpiManagement.Infrastructure.Sispro;

// Forma de GET /api/cotizacion/{numero} de SISPRO (API interna, sin documentar; v0.0.86 al
// 2026-10-27). Sólo se declaran los campos que el mapeo usa: si SISPRO agrega campos no pasa
// nada, y si quita uno el valor llega null y el mapeo lo reporta como advertencia.

public sealed class SisproCotizacionDto
{
    public int Codigo { get; set; }
    public int? CodigoSolicitud { get; set; }
    public string? Referencia { get; set; }
    public DateTimeOffset? FechaCotizacion { get; set; }
    public SisproDescripcionDto? EstadoSolicitud { get; set; }
    public SisproDescripcionDto? UnidadVenta { get; set; }
    public SisproDescripcionDto? Cliente { get; set; }
    public List<SisproCantidadDto>? CotizacionCantidad { get; set; }
    public List<SisproElementoDto>? CotizacionElemento { get; set; }
}

public sealed class SisproDescripcionDto
{
    public string? Descripcion { get; set; }
}

public sealed class SisproCantidadDto
{
    public decimal Cantidad { get; set; }
    public decimal? PrecioUnidadVenta { get; set; }
    public decimal? PrecioTotal { get; set; }
}

/// <summary>Un "elemento" de la cotización (normalmente uno por producto). Las medidas son de imposición.</summary>
public sealed class SisproElementoDto
{
    public decimal? MedidaEje { get; set; }
    public decimal? MedidaDesarrollo { get; set; }
    public int? EtiquetasEje { get; set; }
    public int? EtiquetasDesarrollo { get; set; }
    public decimal? AnchoMaterial { get; set; }
    public int? CantidadColoresFrente { get; set; }
    /// <summary>Colores por el reverso (impresión interna). SISPRO lo llama "liner" por su origen en etiquetas.</summary>
    public int? CantidadColoresLiner { get; set; }
    public List<SisproProcesoDto>? CotizacionElementoProceso { get; set; }
}

public sealed class SisproProcesoDto
{
    public int ItemProceso { get; set; }
    public SisproDescripcionDto? Proceso { get; set; }
    public List<SisproProcesoMaterialDto>? CotizacionElementoProcesoMateriales { get; set; }
}

public sealed class SisproProcesoMaterialDto
{
    public decimal? Espesor { get; set; }
    public decimal? Densidad { get; set; }
    public decimal? Gramaje { get; set; }
    public decimal? Ancho { get; set; }
    public decimal? Largo { get; set; }
    public SisproMaterialDto? Material { get; set; }
}

public sealed class SisproMaterialDto
{
    public string? DescripcionInterna { get; set; }
}
