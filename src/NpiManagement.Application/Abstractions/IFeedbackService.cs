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
    DateTimeOffset? StatusChangedAt);

/// <summary>Lo que ve quien reportó: el seguimiento y la respuesta, sin los datos técnicos.</summary>
public sealed record MyFeedbackDto(
    Guid Id,
    FeedbackKind Kind,
    FeedbackStatus Status,
    string Description,
    string? AdminResponse,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusChangedAt);

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
}
