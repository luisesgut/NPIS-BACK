using System.Globalization;
using System.Text;

namespace NpiManagement.Application.Catalogs;

/// <summary>
/// Lo que el código sabe de los catálogos y no puede vivir en la base:
///  - Qué opciones usan las reglas del formulario por su texto exacto (formConfig.ts). Renombrarlas o
///    desactivarlas rompería esas reglas, así que no se permite.
///  - Qué campos de la captura llenan cada catálogo (para contar cuántos NPIs usan cada opción).
///  - Avisos para quien edita (p. ej. Formato se filtra por su prefijo).
/// Si cambia una regla en formConfig.ts del frontend, hay que actualizarla aquí también.
/// </summary>
public static class CatalogRules
{
    private static readonly Dictionary<string, Dictionary<string, string>> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TIPO DE PRODUCTO"] = new(StringComparer.Ordinal)
        {
            ["BOBINA"] = "El formulario decide qué campos de empaque y qué formatos mostrar según esta opción.",
            ["BOLSA"] = "El formulario decide qué campos de empaque y qué formatos mostrar según esta opción.",
            ["POUCH"] = "El formulario decide qué campos de empaque y qué formatos mostrar según esta opción.",
        },
        ["ACABADO"] = new(StringComparer.Ordinal)
        {
            ["SIN IMPRESION"] = "Con esta opción el formulario oculta los campos de impresión.",
        },
    };

    /// <summary>Campos de la captura (nombre de la propiedad en NpiCaptureData) que se llenan con cada catálogo.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["ACABADO"] = ["Acabado"],
        ["CATEGORÍA SAP"] = ["CategoriaSAP"],
        ["CIERRE"] = ["Cierre"],
        ["COLGADERA"] = ["Colgadera"],
        ["CORE"] = ["Core"],
        ["DESARROLLO MANGA"] = ["DesarrolloManga"],
        ["COSTO DE PREPRENSA"] = ["CostoPreprensa"],
        ["FORMATO"] = ["Formato"],
        ["FORMULA LDPE"] = ["FormulaLDPE_PI", "FormulaLDPE_PPL", "FormulaLDPE_PSL"],
        ["FUELLES"] = ["Fuelles"],
        ["MERCADO"] = ["Mercado"],
        ["OTROS ELEMENTOS"] = ["OtrosElementos"],
        ["PERFORACIONES"] = ["Perforaciones"],
        ["SELLO"] = ["Sello"],
        ["SUB MERCADO"] = ["SubMercado"],
        ["TERMINADO"] = ["TerminadoPI"],
        ["TERMINADO LAMINACION"] = ["TerminadoPPL", "TerminadoPSL"],
        ["TIPO DE CAJA"] = ["TipoCaja"],
        ["TIPO DE ENTRADA"] = ["TipoEntrada"],
        ["TIPO DE PELICULA"] = ["PiTipoPelicula", "PplTipoPelicula", "PslTipoPelicula"],
        ["TIPO DE PRODUCTO"] = ["TipoProducto"],
        ["TIPO EMPAQUE"] = ["TipoEmpaque"],
        ["TIPO IMPRESIÓN"] = ["TipoImpresion"],
        ["TIPO TINTA"] = ["TipoTinta"],
        ["UNIDAD DE VENTA"] = ["UnidadVenta"],
    };

    private static readonly Dictionary<string, string> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DESARROLLO MANGA"] = "Desarrollos (mm) de las mangas de impresión que hay en inventario, sin importar la prensa. Escribe solo el número. El buscador de mangas del formulario usa además el inventario por prensa (lib/mangas.ts del front, copia de la tabla «Inventario de mangas por prensa»): si das de alta un desarrollo nuevo, pide que lo agreguen ahí también.",
        ["FORMATO"] = "El formulario filtra los formatos por cómo empiezan: los que empiezan con «BOBINA» son para Bobina, con «BOLSA» para Bolsa y el resto para Pouch. Respeta ese prefijo al agregar o renombrar.",
        ["TIPO DE PRODUCTO"] = "BOBINA, BOLSA y POUCH están protegidos porque el formulario depende de ellos.",
    };

    /// <summary>Motivo por el que una opción no se puede renombrar ni desactivar; null si es libre.</summary>
    public static string? ProtectionReason(string catalogKey, string value) =>
        Protected.TryGetValue(catalogKey, out var options) && options.TryGetValue(Normalize(value), out var reason) ? reason : null;

    public static string? Hint(string catalogKey) => Hints.GetValueOrDefault(catalogKey);

    /// <summary>Mayúsculas y sin acentos ni espacios repetidos: "Sin  Impresión" y "SIN IMPRESION" son la misma opción.</summary>
    public static string Normalize(string value)
    {
        var decomposed = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    /// <summary>Texto que se guarda: sin espacios sobrantes (se respeta lo que escribieron, acentos incluidos).</summary>
    public static string Clean(string value) => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
