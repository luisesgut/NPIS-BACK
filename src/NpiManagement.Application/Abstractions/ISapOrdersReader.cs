namespace NpiManagement.Application.Abstractions;

/// <summary>Una orden de venta de SAP (vista Cat_Ordenes), sólo lo que Comercial necesita para reconocerla.</summary>
/// <param name="FechaCreacion">Cuándo se creó en SAP (2026-11-27): la más antigua es la que corresponde al NPI.</param>
public sealed record SapOrderDto(
    string Orden, string ClaveProducto, string? Producto, decimal? Cantidad, string? Unidad,
    string? NombreCliente, string? Vendedor, string? Sucursal, DateOnly? FechaCreacion = null);

/// <summary>
/// Resultado de buscar las órdenes de un NPI en SAP (2026-11-12). Enabled=false: la integración está
/// apagada (sin configuración) y Comercial captura el pedido a mano, como siempre. Error: SAP no
/// respondió; tampoco bloquea nada.
/// </summary>
public sealed record SapOrdersLookupDto(bool Enabled, string? ClaveProducto, IReadOnlyList<SapOrderDto> Items, string? Error = null);

/// <summary>
/// Lectura de órdenes de venta de SAP (vista dbo.Cat_Ordenes de BioflexRFID, sólo lectura). La llave es
/// el PT (ClaveProducto); la primera orden (la más antigua) es la que corresponde al NPI.
/// </summary>
public interface ISapOrdersReader
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<SapOrderDto>> FindByProductKeyAsync(string claveProducto, CancellationToken cancellationToken);
}
