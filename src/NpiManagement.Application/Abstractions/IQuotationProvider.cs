namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Cotización de SISPRO traducida al lenguaje del NPI. SISPRO es el cotizador oficial (decisión de TI
/// del 2026-10-27); el NPI sólo la lee para precargar la captura y para ligarla al flujo.
///
/// Campos: claves de captura (camelCase, las de npiFields.ts) → valor ya validado contra los
/// catálogos actuales; nunca trae un valor que el desplegable no tenga. CamposInferidos: los que
/// salieron de una regla aproximada (nombre del material o del proceso) y no de una equivalencia
/// confirmada: el vendedor debe revisarlos. Lo que SISPRO tiene pero el NPI no captura igual
/// (imposición, tintas, cantidades y precios) va aparte, sólo como referencia.
/// </summary>
public sealed record QuotationSnapshot(
    string NumeroCotizacion,
    string Estado,
    string Referencia,
    DateTimeOffset? FechaCotizacion,
    string UnidadVenta,
    IReadOnlyDictionary<string, string> Campos,
    IReadOnlyList<string> CamposInferidos,
    IReadOnlyList<QuotationLayer> Capas,
    QuotationImposition? Imposicion,
    int ColoresInterno,
    int ColoresExterno,
    IReadOnlyList<QuotationQuantity> Cantidades,
    IReadOnlyList<string> Advertencias);

/// <summary>
/// Una capa de película de la cotización. Capa: "PI", "PPL" o "PSL". Origen: "equivalencia" (tabla
/// que administra Ingeniería), "inferido" (regla por nombre) o "sin-equivalencia" (no se pudo traducir).
/// </summary>
public sealed record QuotationLayer(string Capa, string MaterialSispro, decimal? Espesor, string TipoPelicula, string Terminado, string FormulaLdpe, string Origen);

/// <summary>Imposición de SISPRO: medidas de la repetición en eje y desarrollo, no la medida de la bolsa.</summary>
public sealed record QuotationImposition(decimal Eje, decimal Desarrollo, int RepeticionesEje, int RepeticionesDesarrollo, decimal? AnchoMaterial);

/// <summary>Una cantidad cotizada. Precios en la unidad de venta de la cotización; null cuando quien consulta no debe verlos.</summary>
public sealed record QuotationQuantity(decimal Cantidad, decimal? PrecioUnidadVenta, decimal? PrecioTotal);

/// <summary>
/// Proveedor de cotizaciones. La implementación (Infrastructure) lee la API de SISPRO; se diseñó
/// como abstracción para poder cambiar de origen sin tocar Application (ADR-018).
/// </summary>
public interface IQuotationProvider
{
    /// <summary>null si la cotización no existe. Lanza ExternalServiceUnavailableException si SISPRO no responde.</summary>
    Task<QuotationSnapshot?> FindAsync(string numeroCotizacion, CancellationToken cancellationToken);
}
