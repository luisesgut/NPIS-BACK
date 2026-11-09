using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Catalogs;

namespace NpiManagement.Application.Bobinas;

/// <summary>De dónde salió la densidad de una capa; el front lo muestra para que se sepa qué tan firme es el resultado.</summary>
public static class OrigenDensidad
{
    /// <summary>Ingeniería eligió el código exacto.</summary>
    public const string Exacto = "exacto";
    /// <summary>Tipo de película + terminado tienen un registro propio (BOPP + MATE → BOPP MATE).</summary>
    public const string TipoTerminado = "tipo-terminado";
    /// <summary>El terminado no tiene densidad propia; se usó la del tipo de película.</summary>
    public const string Tipo = "tipo";
    /// <summary>PE sin código exacto: densidad provisional según la fórmula LDPE.</summary>
    public const string EstimadoPe = "estimado-pe";
}

public sealed record CapaCaptura(string Capa, string TipoPelicula, string Terminado, string FormulaLdpe, double Micras);

public sealed record CapaResuelta(string Capa, string Material, MaterialBobinaDto Densidad, double Micras, string Origen, string? Aviso);

/// <summary>
/// Traduce una capa de la captura (TipoPelicula/Terminado/FormulaLDPE) a un registro con densidad.
/// Orden: código exacto de Ingeniería → tipo + terminado → estimado del PE por fórmula → solo tipo.
/// Si nada coincide, la capa no se puede calcular (nunca cuenta como densidad 0, error del Excel).
/// </summary>
public static class ResolucionMaterial
{
    private const string Pe = "PE";

    public static CapaResuelta? Resolver(CapaCaptura capa, IReadOnlyList<MaterialBobinaDto> materiales, MaterialBobinaDto? exacto, out string? error)
    {
        error = null;
        var material = Descripcion(capa);

        if (exacto is not null)
        {
            if (exacto.DensidadKgM3 > 0)
                return new(capa.Capa, material, exacto, capa.Micras, OrigenDensidad.Exacto,
                    exacto.IsActive ? null : $"{capa.Capa}: el código {exacto.Codigo} está desactivado en el catálogo; se usó de todos modos.");
            error = $"{capa.Capa}: el código exacto {exacto.Codigo} no tiene densidad válida.";
            return null;
        }

        var activos = materiales.Where(m => m.IsActive && m.DensidadKgM3 > 0).ToList();
        var tipo = CatalogRules.Normalize(capa.TipoPelicula);
        var terminado = SinValor(capa.Terminado) ? "" : CatalogRules.Normalize(capa.Terminado);

        if (terminado.Length > 0)
        {
            var exactoPorTerminado = activos.FirstOrDefault(m => !m.EsEstimado && Igual(m.TipoPelicula, tipo) && Igual(m.Terminado, terminado));
            if (exactoPorTerminado is not null)
                return new(capa.Capa, material, exactoPorTerminado, capa.Micras, OrigenDensidad.TipoTerminado, null);
        }

        if (tipo == Pe)
        {
            var formula = SinValor(capa.FormulaLdpe) ? "" : CatalogRules.Normalize(capa.FormulaLdpe);
            var estimado = activos.FirstOrDefault(m => m.EsEstimado && Igual(m.TipoPelicula, Pe) && formula.Length > 0 && Igual(m.FormulaLdpe, formula))
                           ?? activos.FirstOrDefault(m => m.EsEstimado && Igual(m.TipoPelicula, Pe) && string.IsNullOrWhiteSpace(m.FormulaLdpe));
            if (estimado is not null)
                return new(capa.Capa, material, estimado, capa.Micras, OrigenDensidad.EstimadoPe,
                    $"{capa.Capa}: densidad estimada de PE ({estimado.DensidadKgM3:0.##} kg/m³). Ingeniería debe elegir el código exacto.");
        }

        var porTipo = activos.FirstOrDefault(m => !m.EsEstimado && Igual(m.TipoPelicula, tipo) && string.IsNullOrWhiteSpace(m.Terminado));
        if (porTipo is not null)
            return new(capa.Capa, material, porTipo, capa.Micras, OrigenDensidad.Tipo,
                terminado.Length > 0 ? $"{capa.Capa}: «{capa.TipoPelicula} {capa.Terminado}» no tiene densidad propia; se usó la de {porTipo.Codigo}." : null);

        error = $"{capa.Capa}: no hay densidad registrada para «{material}». Agrégala en Catálogos › Densidades.";
        return null;
    }

    public static bool SinValor(string? value) =>
        string.IsNullOrWhiteSpace(value) || CatalogRules.Normalize(value) is "N/A" or "NA";

    private static string Descripcion(CapaCaptura capa) =>
        SinValor(capa.Terminado) ? capa.TipoPelicula.Trim() : $"{capa.TipoPelicula.Trim()} {capa.Terminado.Trim()}";

    private static bool Igual(string? catalogValue, string normalized) =>
        !string.IsNullOrWhiteSpace(catalogValue) && CatalogRules.Normalize(catalogValue) == normalized;
}
