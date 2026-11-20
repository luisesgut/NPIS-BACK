namespace NpiManagement.Infrastructure.Advance;

/// <summary>
/// Advance, el repositorio de PrintCards de la planta (2026-11-12). Sin contraseña (intranet). Apagado
/// por omisión: con Habilitado=false no se consulta nada y todo se hace a mano.
/// </summary>
public sealed class AdvanceOptions
{
    public const string SectionName = "Advance";

    public bool Habilitado { get; set; }

    /// <summary>
    /// Sólo NPIs creados desde esta fecha se actualizan desde Advance; los anteriores se quedan como
    /// están (se hacen a mano). Por omisión el 13/11/2026, hora de México. Se cambia con Advance__AplicarDesde.
    /// </summary>
    public DateTimeOffset? AplicarDesde { get; set; } = new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.FromHours(-6));

    /// <summary>Estado de los documentos de un PrintCard; se le agrega ?codigo=…</summary>
    public string EstadoUrl { get; set; } = "http://172.16.10.239:908/api/api/ValidacionArchivos/estado";

    /// <summary>PDF vigente del PrintCard; se le agrega /{codigo}.</summary>
    public string ArchivoUrl { get; set; } = "http://172.16.10.31/api/Printcard";

    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>Cada cuánto se revisan los NPIs pendientes contra Advance.</summary>
    public int MinutosEntreRevisiones { get; set; } = 15;

    /// <summary>Los NPIs completados se siguen revisando (por si cambia el PrintCard final) durante estos días.</summary>
    public int DiasCompletadosRevisados { get; set; } = 120;
}
