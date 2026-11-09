using System.Globalization;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.Sispro;

/// <summary>Traducción confirmada por Ingeniería de un material de SISPRO a la película del NPI.</summary>
public sealed record SisproEquivalence(string TipoPelicula, string? Terminado, string? FormulaLdpe);

/// <summary>
/// Traduce una cotización de SISPRO a la captura del NPI. Función pura (sin red ni base de datos)
/// para poder probarla con cotizaciones reales guardadas como ejemplo.
///
/// Reglas, en orden de confianza:
///  1. Equivalencias que administra Ingeniería (material de SISPRO → tipo/terminado/fórmula).
///  2. Reglas por nombre ("PET MET" → PET metalizado). Lo que sale de aquí va en CamposInferidos.
///  3. Lo que no se puede traducir se deja vacío y se explica en Advertencias: nunca se inventa.
/// Todo valor de catálogo se valida contra las opciones activas actuales (los catálogos se
/// administran en producción): si el desplegable no tiene ese valor, no se llena.
///
/// SISPRO viene de etiquetas y no guarda la medida de la bolsa sino la imposición (eje/desarrollo
/// con repeticiones): por eso ancho y alto NO se llenan; la imposición va aparte como referencia.
/// </summary>
public static class SisproQuotationMapper
{
    // Estados de SISPRO en los que la cotización ya tiene precio.
    private static readonly string[] QuotedStates = ["COTIZADA", "ACEPTADA", "CON PROPUESTA"];

    public static bool IsQuoted(string estado) => QuotedStates.Contains(estado.Trim().ToUpperInvariant());

    public static QuotationSnapshot Map(
        SisproCotizacionDto dto,
        IReadOnlyDictionary<string, SisproEquivalence> equivalences,
        IReadOnlyDictionary<string, IReadOnlyList<string>> catalogs,
        IReadOnlyCollection<string>? knownPeople = null)
    {
        var campos = new Dictionary<string, string>();
        var inferidos = new List<string>();
        var advertencias = new List<string>();
        var catalog = new CatalogMatcher(catalogs);

        void Set(string key, string? value, string catalogKey, bool inferred, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var match = catalog.Find(catalogKey, value);
            if (match is null)
            {
                advertencias.Add($"{label}: SISPRO dice «{value}», que no existe en el catálogo del NPI. Selecciónalo a mano.");
                return;
            }
            campos[key] = match;
            if (inferred) inferidos.Add(key);
        }

        var numero = dto.Codigo.ToString(CultureInfo.InvariantCulture);
        campos["numeroCotizacion"] = numero;

        var estado = dto.EstadoSolicitud?.Descripcion?.Trim() ?? "";
        if (!IsQuoted(estado))
            advertencias.Add($"La cotización está «{(estado.Length > 0 ? estado : "sin estado")}» en SISPRO: todavía no tiene precio definitivo.");

        var referencia = dto.Referencia?.Trim().TrimEnd('-', ' ') ?? "";
        var (clienteEnReferencia, descripcion) = SplitReferencia(referencia, knownPeople ?? []);
        if (descripcion.Length > 0)
        {
            campos["descripcion"] = descripcion;
            inferidos.Add("descripcion");
        }

        var cliente = dto.Cliente?.Descripcion?.Trim() ?? "";
        if (cliente.Length > 0 && !cliente.Equals("Cliente", StringComparison.OrdinalIgnoreCase))
        {
            campos["cliente"] = cliente;
            inferidos.Add("cliente");
        }
        else if (clienteEnReferencia is not null)
        {
            campos["cliente"] = clienteEnReferencia;
            inferidos.Add("cliente");
            advertencias.Add($"El cliente se tomó de la referencia de SISPRO («{clienteEnReferencia}»): confírmalo.");
        }
        else
        {
            advertencias.Add("SISPRO no trae el cliente (está como «Cliente» genérico): captúralo a mano.");
        }

        var unidadVenta = dto.UnidadVenta?.Descripcion?.Trim() ?? "";
        Set("unidadVenta", UnidadVentaNpi(unidadVenta), "UNIDAD DE VENTA", inferred: false, "Unidad de venta");

        var elementos = dto.CotizacionElemento ?? [];
        if (elementos.Count > 1)
            advertencias.Add($"La cotización tiene {elementos.Count} elementos; se tomó el primero.");
        var elemento = elementos.FirstOrDefault();

        var procesos = (elemento?.CotizacionElementoProceso ?? []).OrderBy(p => p.ItemProceso).ToList();
        var nombresProceso = procesos.Select(p => Normalize(p.Proceso?.Descripcion)).ToList();
        var materiales = procesos
            .SelectMany(p => (p.CotizacionElementoProcesoMateriales ?? []).Select(m => (Proceso: Normalize(p.Proceso?.Descripcion), Material: m)))
            .ToList();

        // ===== Películas: primero las del proceso de impresión, luego las demás en el orden del proceso. =====
        var peliculas = materiales
            .Where(x => IsFilm(x.Material))
            .OrderBy(x => x.Proceso.StartsWith("IMPRESION", StringComparison.Ordinal) ? 0 : 1)
            .Select(x => x.Material)
            .ToList();
        if (peliculas.Count == 0)
            advertencias.Add("La cotización no trae películas reconocibles: captura la estructura a mano.");
        if (peliculas.Count > 3)
            advertencias.Add($"La cotización trae {peliculas.Count} películas y el NPI sólo maneja 3 capas; se tomaron las primeras 3.");

        var capas = new List<QuotationLayer>();
        string[] prefijos = ["pi", "ppl", "psl"];
        string[] nombresCapa = ["PI", "PPL", "PSL"];
        string[] etiquetas = ["Película de impresión", "Película 1ª laminación", "Película 2ª laminación"];
        for (var i = 0; i < Math.Min(peliculas.Count, 3); i++)
        {
            var material = peliculas[i];
            var nombre = material.Material?.DescripcionInterna?.Trim() ?? "";
            var traduccion = Translate(nombre, isPrintLayer: i == 0, equivalences);
            var prefijo = prefijos[i];
            var terminadoKey = i == 0 ? "terminadoPI" : $"terminado{nombresCapa[i]}";
            var terminadoCatalog = i == 0 ? "TERMINADO" : "TERMINADO LAMINACION";

            var tipo = traduccion is null ? null : catalog.Find("TIPO DE PELICULA", traduccion.TipoPelicula);
            var terminado = traduccion?.Terminado is { } t ? catalog.Find(terminadoCatalog, t) : null;
            var formula = traduccion?.FormulaLdpe is { } f ? catalog.Find("FORMULA LDPE", f) : null;
            var inferred = traduccion is not null && !traduccion.FromEquivalence;

            if (tipo is not null)
            {
                campos[$"{prefijo}TipoPelicula"] = tipo;
                if (inferred) inferidos.Add($"{prefijo}TipoPelicula");
            }
            else
            {
                advertencias.Add($"{etiquetas[i]}: no se pudo traducir «{nombre}». Agrégalo en Equivalencias SISPRO o selecciónalo a mano.");
            }
            if (terminado is not null)
            {
                campos[terminadoKey] = terminado;
                if (inferred) inferidos.Add(terminadoKey);
            }
            if (formula is not null)
            {
                campos[$"formulaLDPE_{nombresCapa[i]}"] = formula;
                if (inferred) inferidos.Add($"formulaLDPE_{nombresCapa[i]}");
            }

            if (material.Espesor is > 0 and var espesor)
                campos[$"{prefijo}Calibre"] = FormatNumber(espesor);
            else
                advertencias.Add($"{etiquetas[i]}: SISPRO no trae el calibre de «{nombre}». Captúralo a mano.");

            capas.Add(new QuotationLayer(nombresCapa[i], nombre, material.Espesor, tipo ?? "", terminado ?? "", formula ?? "",
                traduccion is null || tipo is null ? "sin-equivalencia" : traduccion.FromEquivalence ? "equivalencia" : "inferido"));
        }

        // ===== Impresión =====
        var coloresInterno = elemento?.CantidadColoresLiner ?? 0;
        var coloresExterno = elemento?.CantidadColoresFrente ?? 0;
        if (coloresInterno > 0 && coloresExterno > 0)
            advertencias.Add($"Lleva impresión interna ({coloresInterno} colores) y externa ({coloresExterno}): el NPI sólo guarda una; se tomó la interna.");
        if (coloresInterno > 0) Set("tipoImpresion", "INTERNA (REVERSO)", "TIPO IMPRESIÓN", inferred: false, "Tipo de impresión");
        else if (coloresExterno > 0) Set("tipoImpresion", "EXTERNA (FRENTE)", "TIPO IMPRESIÓN", inferred: false, "Tipo de impresión");

        // ===== Tipo de producto y formato: por los procesos de conversión (la plantilla de SISPRO no es confiable). =====
        var (tipoProducto, formato) = InferProduct(nombresProceso, peliculas.Count);
        Set("tipoProducto", tipoProducto, "TIPO DE PRODUCTO", inferred: true, "Tipo de producto");
        Set("formato", formato, "FORMATO", inferred: true, "Formato");

        // ===== Elementos =====
        if (materiales.Any(x => Normalize(x.Material.Material?.DescripcionInterna).Contains("ZIPPER", StringComparison.Ordinal)))
            Set("cierre", "ZIPPER", "CIERRE", inferred: true, "Cierre");

        if (tipoProducto == "BOBINA")
        {
            var core = materiales.Select(x => Normalize(x.Material.Material?.DescripcionInterna)).FirstOrDefault(n => n.StartsWith("CENTRO", StringComparison.Ordinal));
            if (core is not null && System.Text.RegularExpressions.Regex.Match(core, @"\d+") is { Success: true } pulgadas)
                Set("core", $"{pulgadas.Value}\"", "CORE", inferred: true, "Core");
            if (materiales.Any(x => Normalize(x.Material.Material?.DescripcionInterna).StartsWith("TAPON", StringComparison.Ordinal)))
                campos["taponCore"] = "SI";
        }

        // ===== Referencia: lo que el NPI no captura igual =====
        QuotationImposition? imposicion = null;
        if (elemento is { MedidaEje: > 0, MedidaDesarrollo: > 0 })
        {
            imposicion = new QuotationImposition(elemento.MedidaEje.Value, elemento.MedidaDesarrollo.Value,
                elemento.EtiquetasEje ?? 1, elemento.EtiquetasDesarrollo ?? 1, elemento.AnchoMaterial);
            advertencias.Add("Ancho y alto no se llenan: SISPRO guarda la imposición (eje × desarrollo con repeticiones), no la medida de la bolsa. Revisa la referencia y captúralos.");
        }

        var cantidades = (dto.CotizacionCantidad ?? [])
            .Select(c => new QuotationQuantity(c.Cantidad, c.PrecioUnidadVenta, c.PrecioTotal))
            .ToList();

        return new QuotationSnapshot(numero, estado, referencia, dto.FechaCotizacion, unidadVenta, campos, inferidos.Distinct().ToList(),
            capas, imposicion, coloresInterno, coloresExterno, cantidades, advertencias);
    }

    // Palabras con las que empieza la descripción del producto en la referencia de SISPRO.
    private static readonly HashSet<string> ProductWords =
    [
        "BOLSA", "BOLSAS", "BOB", "BOBINA", "BOBINAS", "POUCH", "ROLLSTOCK", "ROLLO", "LAMINA", "LAMINADO", "LAMINADA",
        "ETIQUETA", "SOBRE", "FUNDA", "MANGA", "PELICULA", "STAND", "STANDUP", "DOYPACK", "WICKET", "EMPAQUE",
    ];

    /// <summary>
    /// SISPRO deja el cliente como «Cliente» genérico y los vendedores lo escriben en la referencia, cada
    /// quien a su manera: «PROAN - BOLSA POUCH…» o «LUIS VEGA - CRESTOM HILL - BOLSA POUCH… - CAMARON…».
    /// La descripción empieza en el primer tramo que arranca con una palabra de producto (BOLSA, BOB…) y
    /// llega hasta el final; el cliente es el primer tramo anterior que no es una persona del sistema (el
    /// vendedor). Sin palabra de producto, la descripción es el último tramo. Es una deducción: quien llama
    /// la marca para revisar.
    /// </summary>
    public static (string? Cliente, string Descripcion) SplitReferencia(string referencia, IReadOnlyCollection<string> knownPeople)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(referencia, @"\s+[-–]\s+")
            .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        if (parts.Count <= 1) return (null, referencia.Trim());

        var productStart = parts.FindIndex(p => ProductWords.Contains(Normalize(p).Split(' ')[0]));
        if (productStart < 0) productStart = parts.Count - 1;
        var descripcion = string.Join(" - ", parts.Skip(productStart));
        if (productStart == 0) return (null, descripcion);

        var cliente = parts.Take(productStart).FirstOrDefault(p => !IsPerson(p, knownPeople));
        return (cliente, descripcion);
    }

    /// <summary>
    /// ¿El tramo es el nombre de un usuario del sistema? Cada palabra debe ser inicio de alguna palabra
    /// del nombre («DAN NAVARRO» ≈ «Daniel Navarro»). Un tramo de una sola palabra no se toma por
    /// persona: los clientes suelen ser una sola palabra (PROAN, COVEMEX, EMBAG).
    /// </summary>
    private static bool IsPerson(string segment, IReadOnlyCollection<string> knownPeople)
    {
        var tokens = Normalize(segment).Split(' ');
        if (tokens.Length < 2) return false;
        return knownPeople.Any(person =>
        {
            var personTokens = Normalize(person).Split(' ');
            return tokens.All(t => personTokens.Any(pt => pt.StartsWith(t, StringComparison.Ordinal)));
        });
    }

    /// <summary>Normaliza nombres de SISPRO para comparar: mayúsculas, sin acentos ni espacios dobles.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decomposed = value.Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Película: tiene densidad o espesor y no es un gramaje (adhesivo, tinta). Core y tapón no tienen densidad.
    private static bool IsFilm(SisproProcesoMaterialDto m) =>
        (m.Densidad is > 0 || m.Espesor is > 0) && m.Gramaje is null or 0;

    private sealed record Translation(string TipoPelicula, string? Terminado, string? FormulaLdpe, bool FromEquivalence);

    private static Translation? Translate(string material, bool isPrintLayer, IReadOnlyDictionary<string, SisproEquivalence> equivalences)
    {
        var name = Normalize(material);
        if (equivalences.TryGetValue(name, out var eq))
            return new Translation(eq.TipoPelicula, eq.Terminado, eq.FormulaLdpe, FromEquivalence: true);

        var tipo = InferFilmType(name);
        if (tipo is null) return null;

        // El terminado de la impresión (TERMINADO) describe el aspecto; el de las laminaciones
        // (TERMINADO LAMINACION) describe una propiedad especial. Son catálogos distintos.
        string? terminado = isPrintLayer
            ? Contains(name, "MET") ? "METALIZADO"
              : Contains(name, "MAT") ? "MATE"
              : Contains(name, "BCO", "BLANCO", "PIGM") ? "BLANCO"
              : Contains(name, "NAT", "TRANSP") ? "TRANSPARENTE"
              : null
            : Contains(name, "ANTIFOG", "AFOG") ? "ANTIFOG"
              : Contains(name, "ALOX") ? "ALOX"
              : Contains(name, "PELABLE") ? "PELABLE"
              : Contains(name, "OXO") ? "OXODEGRADABLE"
              : Contains(name, "CAV") ? "CAVITADO"
              : Contains(name, "RETORT") ? "RETORTABLE"
              : null;

        string? formula = tipo != "PE" ? null
            : Contains(name, "RESPALDO", "LAM") ? "LAMINACIÓN"
            : Contains(name, "CONGEL") ? "CONGELADO"
            : Contains(name, "ALTA CLARIDAD", "PAN", "LECHUGA") ? "ALTA CLARIDAD"
            : Contains(name, "AUTOM") ? "EMPAQUE AUTOMÁTICO"
            : Contains(name, "SELLO") ? "ALTO SELLO"
            : Contains(name, "COMERCIAL") ? "COMERCIAL"
            : null;

        return new Translation(tipo, terminado, formula, FromEquivalence: false);
    }

    private static string? InferFilmType(string name) =>
        name.StartsWith("PET", StringComparison.Ordinal) ? "PET"
        : name.StartsWith("BOPP", StringComparison.Ordinal) ? "BOPP"
        : name.StartsWith("CPP", StringComparison.Ordinal) ? "CPP"
        : name.StartsWith("BOPA", StringComparison.Ordinal) || Contains(name, "NYLON") ? "BOPA"
        : Contains(name, "FOIL", "ALUMINIO") ? "FOIL"
        : name.StartsWith("MDO", StringComparison.Ordinal) ? "MDO"
        : name.StartsWith("LDPE", StringComparison.Ordinal) || name.StartsWith("HDPE", StringComparison.Ordinal)
          || name.StartsWith("PE ", StringComparison.Ordinal) || name == "PE" || Contains(name, "COEX") ? "PE"
        : null;

    private static (string? TipoProducto, string? Formato) InferProduct(IReadOnlyList<string> procesos, int peliculas)
    {
        bool Has(params string[] parts) => procesos.Any(p => parts.Any(part => p.Contains(part, StringComparison.Ordinal)));

        if (Has("POUCH 2 SELLOS")) return ("POUCH", "POUCH 2 SELLOS");
        if (Has("POUCH 3 SELLOS")) return ("POUCH", "POUCH 3 SELLOS");
        if (Has("DOY")) return ("POUCH", "DOYPACK (SELLO \"U\"/SONRISA)");
        if (Has("STAND UP", "STANDUP", "SELLO K")) return ("POUCH", "STANDUP POUCH (SELLO K)");
        if (Has("WICKET")) return ("BOLSA", "BOLSA WICKET");
        if (Has("SELLO LATERAL")) return ("BOLSA", "BOLSA SELLO LATERAL");
        if (Has("SELLO DE FONDO", "SELLO FONDO")) return ("BOLSA", "BOLSA SELLO DE FONDO");
        if (Has("POUCH")) return ("POUCH", null);
        if (Has("BOLSA", "BOLSEO")) return ("BOLSA", null);
        if (Has("CORTE", "REFIL")) return ("BOBINA", peliculas >= 2 ? "BOBINA LAMINADA" : "BOBINA PLANA");
        return (null, null);
    }

    private static string? UnidadVentaNpi(string sispro) => Normalize(sispro) switch
    {
        "MILLAR" or "MIL" or "MILLARES" => "MILL",
        "KILO" or "KILOS" or "KG" or "KILOGRAMO" => "KG",
        "PIEZA" or "PIEZAS" or "PZA" or "PZAS" => "PZAS",
        "" => null,
        _ => "OTRO",
    };

    private static bool Contains(string name, params string[] parts) =>
        parts.Any(p => name.Contains(p, StringComparison.Ordinal));

    private static string FormatNumber(decimal value) =>
        (value % 1 == 0 ? decimal.Truncate(value) : value).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Busca un valor en las opciones activas de un catálogo sin importar mayúsculas ni acentos (ni en
    /// la clave del catálogo ni en el valor); devuelve la opción tal cual está en el catálogo.
    /// </summary>
    private sealed class CatalogMatcher(IReadOnlyDictionary<string, IReadOnlyList<string>> catalogs)
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _byKey =
            catalogs.GroupBy(c => Normalize(c.Key)).ToDictionary(g => g.Key, g => g.First().Value);

        public string? Find(string catalogKey, string value)
        {
            if (!_byKey.TryGetValue(Normalize(catalogKey), out var options)) return null;
            var wanted = Normalize(value);
            return options.FirstOrDefault(o => Normalize(o) == wanted);
        }
    }
}
