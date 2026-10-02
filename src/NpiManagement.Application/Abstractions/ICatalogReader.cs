namespace NpiManagement.Application.Abstractions;

public sealed record CatalogOptionDto(string Value, bool IsActive);

public sealed record CatalogDto(string Key, string DisplayName, IReadOnlyList<CatalogOptionDto> Options);

/// <summary>Catálogos administrables (docs/api-contract.md §8, ADR-020) — lectura para cualquier usuario autenticado.</summary>
public interface ICatalogReader
{
    Task<IReadOnlyList<CatalogDto>> ListAsync(CancellationToken cancellationToken);
}
