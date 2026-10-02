namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>
/// Catálogo administrable (ver ADR-020 — lista de opciones de negocio, no un enum de dominio).
/// Sustituye a los 19 catálogos hardcodeados en src/data/catalogs.ts del frontend.
/// </summary>
public sealed class Catalog
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public List<CatalogOption> Options { get; set; } = [];
}

public sealed class CatalogOption
{
    public Guid Id { get; set; }
    public Guid CatalogId { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Catalog Catalog { get; set; } = null!;
}
