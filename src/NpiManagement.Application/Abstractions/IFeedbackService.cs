using NpiManagement.Application.Authorization;
using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

/// <summary>Límites compartidos por el contrato HTTP, la validación del servicio y el esquema de base de datos.</summary>
public static class FeedbackLimits
{
    public const int DescriptionMinLength = 5;
    public const int DescriptionMaxLength = 4000;
    public const int AdminResponseMaxLength = 2000;
    public const int PageUrlMaxLength = 500;
    public const int UserAgentMaxLength = 500;
    public const int ScreenSizeMaxLength = 50;
    public const int TechnicalDetailsMaxLength = 8000;
    public const int MessageMaxLength = 2000;
}

/// <summary>Captura de pantalla adjunta a un reporte. Sólo PNG o JPG, validada por firma binaria.</summary>
public sealed record FeedbackScreenshot(Stream Content, string ContentType, long DeclaredSizeBytes);

/// <summary>
/// Lo que llega del botón "Reportar". Salvo el tipo y la descripción, todo lo captura la app sola
/// (página, navegador, errores recientes) — el usuario no tiene que saber explicarlo.
/// </summary>
public sealed record SubmitFeedbackRequest(
    FeedbackKind Kind,
    string Description,
    string? PageUrl,
    Guid? NpiId,
    string? UserAgent,
    string? ScreenSize,
    string? TechnicalDetails,
    FeedbackScreenshot? Screenshot);

/// <summary>Vista completa para el AdminTotal: quién reportó, desde dónde y con qué datos técnicos.</summary>
public sealed record FeedbackReportDto(
    Guid Id,
    FeedbackKind Kind,
    FeedbackStatus Status,
    string Description,
    string? PageUrl,
    Guid? NpiId,
    Guid ReporterUserId,
    string ReporterName,
    string ReporterEmail,
    string ReporterRole,
    string? UserAgent,
    string? ScreenSize,
    string? TechnicalDetails,
    bool HasScreenshot,
    string? AdminResponse,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusChangedAt,
    /// <summary>Mensajes en el hilo con soporte (2026-11-10).</summary>
    int MessageCount = 0,
    DateTimeOffset? LastMessageAt = null,
    /// <summary>El último mensaje lo escribió quien reportó: está esperando a soporte.</summary>
    bool AwaitingSupport = false);

/// <summary>Lo que ve quien reportó: el seguimiento y la respuesta, sin los datos técnicos.</summary>
public sealed record MyFeedbackDto(
    Guid Id,
    FeedbackKind Kind,
    FeedbackStatus Status,
    string Description,
    string? AdminResponse,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusChangedAt,
    int MessageCount = 0,
    DateTimeOffset? LastMessageAt = null,
    /// <summary>El último mensaje es de soporte: quien reportó tiene respuesta nueva que leer.</summary>
    bool LastFromSupport = false);

/// <summary>Un mensaje del hilo usuario ↔ soporte de un reporte.</summary>
public sealed record FeedbackMessageDto(Guid Id, Guid AuthorUserId, string AuthorName, bool FromSupport, string Text, DateTimeOffset CreatedAt);

/// <summary>Archivo de Excel generado (reportes con su estado).</summary>
public sealed record FeedbackExportFile(byte[] Content, string ContentType, string FileName);

public sealed record FeedbackScreenshotFile(Stream Content, string ContentType, string FileName);

/// <summary>
/// Reportes de problemas, sugerencias y dudas enviados desde la app. Los reportes completos (con
/// captura y datos técnicos) sólo los consulta el AdminTotal; quien reporta sólo ve el estado de
/// los suyos.
/// </summary>
public interface IFeedbackService
{
    /// <summary>Guarda el reporte (y su captura, si trae) y avisa a los AdminTotal activos.</summary>
    Task<Guid> SubmitAsync(CurrentUser reporter, string reporterName, SubmitFeedbackRequest request, CancellationToken cancellationToken);

    /// <summary>Reportes más recientes primero, opcionalmente filtrados por estado.</summary>
    Task<IReadOnlyList<FeedbackReportDto>> ListAsync(FeedbackStatus? status, CancellationToken cancellationToken);

    Task<IReadOnlyList<MyFeedbackDto>> ListMineAsync(Guid reporterUserId, CancellationToken cancellationToken);

    /// <summary>Cambia el estado y la respuesta. Si el estado cambió, avisa a quien reportó.</summary>
    Task UpdateStatusAsync(Guid id, FeedbackStatus status, string? adminResponse, Guid changedByUserId, CancellationToken cancellationToken);

    Task<FeedbackScreenshotFile> OpenScreenshotAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hilo del reporte. Sólo quien lo hizo o un AdminTotal; para cualquier otro, no existe.</summary>
    Task<IReadOnlyList<FeedbackMessageDto>> ListMessagesAsync(Guid reportId, CurrentUser actor, CancellationToken cancellationToken);

    /// <summary>Agrega un mensaje y avisa por campana a la otra parte (quien reportó o los AdminTotal).</summary>
    Task<FeedbackMessageDto> AddMessageAsync(Guid reportId, CurrentUser actor, string actorName, string text, CancellationToken cancellationToken);

    /// <summary>Excel con el concentrado de reportes (sólo AdminTotal), opcionalmente filtrado por estado.</summary>
    Task<FeedbackExportFile> ExportAsync(FeedbackStatus? status, CancellationToken cancellationToken);
}
