namespace NpiManagement.Infrastructure.Sap;

/// <summary>
/// Conexión de sólo lectura a la base con las vistas de SAP (dbo.Cat_Ordenes y dbo.Cat_Productos, base
/// BioflexRFID, 2026-11-12). La cadena de conexión la captura Luis directamente en el servidor
/// (web.config o variable de entorno SapOrders__ConnectionString) — nunca en el repositorio. Sin
/// cadena o con Habilitado=false la integración queda apagada y el pedido se captura a mano.
/// </summary>
public sealed class SapOrdersOptions
{
    public const string SectionName = "SapOrders";

    public bool Habilitado { get; set; }
    public string? ConnectionString { get; set; }

    /// <summary>La vista tarda ~5 s en traer todo; filtrada por PT, mucho menos. Más de esto, se captura a mano.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    public bool IsConfigured => Habilitado && !string.IsNullOrWhiteSpace(ConnectionString);
}
