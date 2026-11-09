using System.Globalization;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Application.Npis;

/// <summary>
/// Compara la estructura cotizada en SISPRO contra la captura del NPI (la que validó Ingeniería).
/// Devuelve una diferencia por renglón, en lenguaje para el vendedor; vacía si coinciden. Sólo
/// compara lo que SISPRO sí trae: tipo de película y calibre por capa, y el formato del producto.
/// </summary>
public static class SisproStructureComparer
{
    // Diferencias de calibre menores a esto son redondeo (37.5 vs 37), no otra película.
    private const decimal CalibreTolerance = 1m;

    public static IReadOnlyList<string> Compare(QuotationSnapshot quote, NpiCaptureData capture)
    {
        var warnings = new List<string>();
        (string Capa, string Etiqueta, string Tipo, string Calibre)[] layers =
        [
            ("PI", "Película de impresión", capture.PiTipoPelicula, capture.PiCalibre),
            ("PPL", "1ª laminación", capture.PplTipoPelicula, capture.PplCalibre),
            ("PSL", "2ª laminación", capture.PslTipoPelicula, capture.PslCalibre),
        ];

        foreach (var (capa, etiqueta, tipoNpi, calibreNpi) in layers)
        {
            var quoted = quote.Capas.FirstOrDefault(c => c.Capa == capa);
            var npiHasLayer = HasValue(tipoNpi);

            if (quoted is null)
            {
                if (npiHasLayer) warnings.Add($"{etiqueta}: el NPI lleva {tipoNpi} y la cotización no trae esa capa.");
                continue;
            }

            if (!npiHasLayer)
            {
                warnings.Add($"{etiqueta}: la cotización trae «{quoted.MaterialSispro}» y el NPI no lleva esa capa.");
                continue;
            }

            if (quoted.TipoPelicula.Length == 0)
                warnings.Add($"{etiqueta}: no se pudo traducir «{quoted.MaterialSispro}» de SISPRO; compáralo a mano contra {tipoNpi}.");
            else if (!string.Equals(quoted.TipoPelicula, tipoNpi.Trim(), StringComparison.OrdinalIgnoreCase))
                warnings.Add($"{etiqueta}: se cotizó {quoted.TipoPelicula} («{quoted.MaterialSispro}») y el NPI dice {tipoNpi}.");

            if (quoted.Espesor is { } espesor && TryParse(calibreNpi, out var calibre) && Math.Abs(espesor - calibre) > CalibreTolerance)
                warnings.Add($"{etiqueta}: se cotizó a {Format(espesor)} micras y el NPI dice {Format(calibre)}.");
        }

        if (quote.Campos.TryGetValue("formato", out var formato) && HasValue(capture.Formato)
            && !string.Equals(formato, capture.Formato.Trim(), StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Formato: los procesos de la cotización indican {formato} y el NPI dice {capture.Formato}.");

        return warnings;
    }

    private static bool HasValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !string.Equals(value.Trim(), "N/A", StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string? value, out decimal result) =>
        decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
