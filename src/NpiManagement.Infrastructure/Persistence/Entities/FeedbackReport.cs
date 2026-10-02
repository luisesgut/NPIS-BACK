using NpiManagement.Domain;

namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>
/// Reporte de problema, sugerencia o duda enviado desde la app. Entidad de datos simple, igual que
/// <see cref="Notification"/>: no participa del workflow del NPI, sólo lo documenta.
/// </summary>
public sealed class FeedbackReport
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }

    /// <summary>
    /// Nombre y rol al momento de reportar. Se copian en vez de resolverse al consultar: si la
    /// persona cambia de rol después, el reporte debe seguir diciendo desde qué rol vio el problema.
    /// </summary>
    public string ReporterName { get; set; } = string.Empty;
    public string ReporterRole { get; set; } = string.Empty;

    public FeedbackKind Kind { get; set; }
    public FeedbackStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Contexto que la app captura sola. NpiId es deliberadamente un dato informativo sin llave
    /// foránea: viene de la URL que estaba viendo el usuario, y un reporte nunca debe fallar al
    /// guardarse porque esa URL apuntara a algo que no existe.
    /// </summary>
    public string? PageUrl { get; set; }
    public Guid? NpiId { get; set; }
    public string? UserAgent { get; set; }
    public string? ScreenSize { get; set; }
    public string? TechnicalDetails { get; set; }

    public string? ScreenshotStorageKey { get; set; }
    public string? ScreenshotContentType { get; set; }

    public string? AdminResponse { get; set; }
    public Guid? StatusChangedByUserId { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
