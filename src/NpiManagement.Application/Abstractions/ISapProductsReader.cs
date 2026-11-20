namespace NpiManagement.Application.Abstractions;

/// <summary>Un artículo de SAP (vista Cat_Productos): el PT y su nombre, ligados al PrintCard.</summary>
public sealed record SapProductDto(string ClaveProducto, string? NombreProducto, string? PrintCard, string? Estatus);

/// <summary>
/// PT en SAP para el PrintCard del NPI (2026-11-12). Approximate: no hubo coincidencia exacta del código
/// y se buscó sin la revisión ("E-11854-A" en lugar de "E-11854-A_R-2"); Ingeniería debe revisarlo.
/// </summary>
public sealed record SapProductsLookupDto(bool Enabled, string? PrintCardCode, IReadOnlyList<SapProductDto> Items, bool Approximate = false, string? Error = null);

/// <summary>Lectura de artículos de SAP (vista dbo.Cat_Productos de BioflexRFID, sólo lectura) por el código del PrintCard.</summary>
public interface ISapProductsReader
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<SapProductDto>> FindByPrintCardAsync(string printCardCode, bool startsWith, CancellationToken cancellationToken);
}
