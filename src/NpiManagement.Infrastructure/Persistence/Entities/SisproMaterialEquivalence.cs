namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>
/// Traducción de un material de SISPRO ("LDPE RESPALDO LAM / GENERICO") a la película del NPI
/// (tipo, terminado y fórmula). La administra Ingeniería; manda sobre las reglas por nombre del
/// mapeo. MaterialSispro se guarda normalizado (mayúsculas, sin acentos) para que la búsqueda no
/// dependa de cómo lo escribieron en SISPRO.
/// </summary>
public sealed class SisproMaterialEquivalence
{
    public Guid Id { get; set; }
    public string MaterialSispro { get; set; } = string.Empty;
    public string TipoPelicula { get; set; } = string.Empty;
    public string? Terminado { get; set; }
    public string? FormulaLdpe { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedByName { get; set; } = string.Empty;
}
