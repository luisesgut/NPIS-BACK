namespace NpiManagement.Application.Abstractions;

public sealed record SisproEquivalenceDto(Guid Id, string MaterialSispro, string TipoPelicula, string? Terminado, string? FormulaLdpe, DateTimeOffset UpdatedAt, string UpdatedByName);

/// <summary>Configured: hay usuario y contraseña en el servidor. Connected: el login de prueba funcionó. Message: qué hacer si no.</summary>
public sealed record SisproStatusDto(bool Configured, bool Connected, string Message);

/// <summary>
/// Administración de la integración con SISPRO: la tabla de equivalencias de materiales (la
/// mantiene Ingeniería) y la prueba de conexión (para TI, después de capturar las credenciales).
/// </summary>
public interface ISisproAdminService
{
    Task<IReadOnlyList<SisproEquivalenceDto>> ListEquivalencesAsync(CancellationToken cancellationToken);

    /// <summary>Crea o reemplaza la equivalencia del material (el nombre se normaliza). Valida contra los catálogos.</summary>
    Task<SisproEquivalenceDto> UpsertEquivalenceAsync(string materialSispro, string tipoPelicula, string? terminado, string? formulaLdpe, string actorName, CancellationToken cancellationToken);

    Task DeleteEquivalenceAsync(Guid id, CancellationToken cancellationToken);

    Task<SisproStatusDto> CheckStatusAsync(CancellationToken cancellationToken);
}
