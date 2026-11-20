namespace NpiManagement.Application.Abstractions;

/// <summary>Un documento del PrintCard en Advance (PrintCard, Especificación técnica = ET, montaje…).</summary>
public sealed record AdvanceDocumentDto(string Tipo, string Nombre, bool Subido);

/// <summary>
/// Estado de un PrintCard en Advance, el repositorio de toda la planta (2026-11-12). ArchivoUrl: dónde
/// ver el PDF vigente. Sin fechas: Advance sólo dice qué está subido.
/// </summary>
public sealed record AdvanceStatusDto(
    string Codigo, bool ExisteEnCatalogo, string? Descripcion, string? CodigoSap,
    int Subidos, int TotalDocumentos, bool Completo, IReadOnlyList<AdvanceDocumentDto> Documentos, string? ArchivoUrl)
{
    public const string PrintCardType = "printcard";
    /// <summary>La "Especificación técnica" de Advance es la ET (confirmado por Luis, 2026-11-12).</summary>
    public const string EtType = "fichaTecnica";

    public bool PrintCardSubido => Documentos.Any(d => d.Subido && string.Equals(d.Tipo, PrintCardType, StringComparison.OrdinalIgnoreCase));
    public bool EtSubida => Documentos.Any(d => d.Subido && string.Equals(d.Tipo, EtType, StringComparison.OrdinalIgnoreCase));
}

/// <summary>El PDF vigente del PrintCard en Advance.</summary>
public sealed record AdvanceFile(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Lectura de Advance: estado de los documentos de un PrintCard y su PDF vigente. Sin configuración
/// (Advance:Habilitado=false) está apagado y todo se hace a mano, como siempre.
/// </summary>
public interface IAdvanceClient
{
    bool IsEnabled { get; }

    /// <summary>
    /// Sólo los NPIs creados desde esta fecha se actualizan solos (2026-11-12, decisión de Luis): los ya
    /// creados se quedan como están, sin versiones nuevas del PrintCard ni avisos de golpe. null = todos.
    /// </summary>
    DateTimeOffset? AppliesFrom { get; }

    /// <summary>null si el PrintCard no está en Advance.</summary>
    Task<AdvanceStatusDto?> GetStatusAsync(string printCardCode, CancellationToken cancellationToken);

    /// <summary>null si Advance no tiene el archivo.</summary>
    Task<AdvanceFile?> DownloadPrintCardAsync(string printCardCode, CancellationToken cancellationToken);
}
