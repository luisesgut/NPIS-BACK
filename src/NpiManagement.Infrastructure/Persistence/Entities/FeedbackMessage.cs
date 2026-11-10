namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>
/// Mensaje del hilo de un reporte entre quien lo hizo y soporte (AdminTotal), 2026-11-10. Antes sólo
/// había una "respuesta del administrador" y el usuario no podía contestar ni aclarar.
/// </summary>
public sealed class FeedbackMessage
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public Guid AuthorUserId { get; set; }

    /// <summary>Nombre al momento de escribir (igual que ReporterName en el reporte).</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>true = lo escribió soporte (AdminTotal); false = quien reportó.</summary>
    public bool FromSupport { get; set; }

    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
