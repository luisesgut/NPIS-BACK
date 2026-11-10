using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Attachments;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Export;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Services;

public sealed class FeedbackService(
    NpiManagementDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    IFileStorage fileStorage,
    IAttachmentPolicy attachmentPolicy,
    INotificationService notificationService,
    IClock clock,
    ILogger<FeedbackService> logger) : IFeedbackService
{
    /// <summary>
    /// Tope de la bandeja. El volumen esperado son decenas de reportes, no miles; el tope sólo
    /// evita que un bucle accidental convierta la página del admin en una consulta gigante.
    /// </summary>
    private const int ListLimit = 500;

    private static readonly Dictionary<FeedbackKind, string> KindLabels = new()
    {
        [FeedbackKind.Bug] = "Error",
        [FeedbackKind.Suggestion] = "Sugerencia",
        [FeedbackKind.Question] = "Duda",
    };

    private static readonly Dictionary<FeedbackStatus, string> StatusLabels = new()
    {
        [FeedbackStatus.New] = "Nuevo",
        [FeedbackStatus.InReview] = "En revisión",
        [FeedbackStatus.Resolved] = "Resuelto",
        [FeedbackStatus.Dismissed] = "Descartado",
    };

    public async Task<Guid> SubmitAsync(CurrentUser reporter, string reporterName, SubmitFeedbackRequest request, CancellationToken cancellationToken)
    {
        var description = request.Description.Trim();
        if (description.Length < FeedbackLimits.DescriptionMinLength)
            throw new RequestValidationException("feedback-description-too-short", $"Describe el problema o la sugerencia con al menos {FeedbackLimits.DescriptionMinLength} caracteres.");
        if (description.Length > FeedbackLimits.DescriptionMaxLength)
            throw new RequestValidationException("feedback-description-too-long", $"La descripción no puede pasar de {FeedbackLimits.DescriptionMaxLength} caracteres.");

        string? screenshotKey = null;
        string? screenshotContentType = null;
        if (request.Screenshot is not null)
            (screenshotKey, screenshotContentType) = await StoreScreenshotAsync(request.Screenshot, cancellationToken);

        // Los datos de contexto se recortan en vez de rechazarse: los junta la app sola, y un
        // user-agent inusualmente largo nunca debe impedir que alguien reporte un problema.
        var report = new FeedbackReport
        {
            Id = Guid.NewGuid(),
            ReporterUserId = reporter.UserId,
            ReporterName = Truncate(reporterName, 200) ?? string.Empty,
            ReporterRole = reporter.Role,
            Kind = request.Kind,
            Status = FeedbackStatus.New,
            Description = description,
            PageUrl = Truncate(request.PageUrl, FeedbackLimits.PageUrlMaxLength),
            NpiId = request.NpiId,
            UserAgent = Truncate(request.UserAgent, FeedbackLimits.UserAgentMaxLength),
            ScreenSize = Truncate(request.ScreenSize, FeedbackLimits.ScreenSizeMaxLength),
            TechnicalDetails = Truncate(request.TechnicalDetails, FeedbackLimits.TechnicalDetailsMaxLength),
            ScreenshotStorageKey = screenshotKey,
            ScreenshotContentType = screenshotContentType,
            CreatedAt = clock.UtcNow,
        };
        dbContext.FeedbackReports.Add(report);

        var admins = await userManager.GetUsersInRoleAsync(ApplicationRoles.AdminTotal);
        var recipients = admins
            .Where(a => a.Status == UserStatus.Active && a.Id != reporter.UserId)
            .Select(a => a.Id)
            .ToList();

        if (recipients.Count == 0)
            logger.LogWarning("Reporte {ReportId} guardado, pero no hay otro AdminTotal activo a quien avisar.", report.Id);

        // Un error bloquea a alguien y amerita correo; una sugerencia o una duda pueden esperar a
        // que el admin abra la campana (ver la advertencia de ruido en NotificationChannel).
        var channel = request.Kind == FeedbackKind.Bug ? NotificationChannel.BellAndEmail : NotificationChannel.Bell;
        notificationService.NotifyMany(
            recipients,
            $"Nuevo reporte ({KindLabels[request.Kind]}) de {report.ReporterName}: {Excerpt(description, 150)}",
            null,
            channel);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Reporte {ReportId} de tipo {Kind} creado por {UserId}.", report.Id, report.Kind, reporter.UserId);
        return report.Id;
    }

    public async Task<IReadOnlyList<FeedbackReportDto>> ListAsync(FeedbackStatus? status, CancellationToken cancellationToken)
    {
        var reports = dbContext.FeedbackReports.AsNoTracking();
        if (status is not null)
            reports = reports.Where(f => f.Status == status.Value);

        var result = await reports
            .Join(dbContext.Users.AsNoTracking(), f => f.ReporterUserId, u => u.Id, (f, u) => new { Report = f, u.Email })
            .OrderByDescending(x => x.Report.CreatedAt)
            .Take(ListLimit)
            .Select(x => new FeedbackReportDto(
                x.Report.Id,
                x.Report.Kind,
                x.Report.Status,
                x.Report.Description,
                x.Report.PageUrl,
                x.Report.NpiId,
                x.Report.ReporterUserId,
                x.Report.ReporterName,
                x.Email ?? string.Empty,
                x.Report.ReporterRole,
                x.Report.UserAgent,
                x.Report.ScreenSize,
                x.Report.TechnicalDetails,
                x.Report.ScreenshotStorageKey != null,
                x.Report.AdminResponse,
                x.Report.CreatedAt,
                x.Report.StatusChangedAt,
                dbContext.FeedbackMessages.Count(m => m.ReportId == x.Report.Id),
                dbContext.FeedbackMessages.Where(m => m.ReportId == x.Report.Id).Max(m => (DateTimeOffset?)m.CreatedAt),
                dbContext.FeedbackMessages.Where(m => m.ReportId == x.Report.Id).OrderByDescending(m => m.CreatedAt).Select(m => (bool?)!m.FromSupport).FirstOrDefault() ?? false))
            .ToListAsync(cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<MyFeedbackDto>> ListMineAsync(Guid reporterUserId, CancellationToken cancellationToken)
    {
        var result = await dbContext.FeedbackReports
            .AsNoTracking()
            .Where(f => f.ReporterUserId == reporterUserId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(ListLimit)
            .Select(f => new MyFeedbackDto(f.Id, f.Kind, f.Status, f.Description, f.AdminResponse, f.CreatedAt, f.StatusChangedAt,
                dbContext.FeedbackMessages.Count(m => m.ReportId == f.Id),
                dbContext.FeedbackMessages.Where(m => m.ReportId == f.Id).Max(m => (DateTimeOffset?)m.CreatedAt),
                dbContext.FeedbackMessages.Where(m => m.ReportId == f.Id).OrderByDescending(m => m.CreatedAt).Select(m => (bool?)m.FromSupport).FirstOrDefault() ?? false))
            .ToListAsync(cancellationToken);

        return result;
    }

    public async Task UpdateStatusAsync(Guid id, FeedbackStatus status, string? adminResponse, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var report = await dbContext.FeedbackReports.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundException("Reporte", id);

        var response = string.IsNullOrWhiteSpace(adminResponse) ? null : adminResponse.Trim();
        if (response?.Length > FeedbackLimits.AdminResponseMaxLength)
            throw new RequestValidationException("feedback-response-too-long", $"La respuesta no puede pasar de {FeedbackLimits.AdminResponseMaxLength} caracteres.");

        var statusChanged = report.Status != status;
        report.Status = status;
        report.AdminResponse = response;
        report.StatusChangedAt = clock.UtcNow;
        report.StatusChangedByUserId = changedByUserId;

        // Cerrar el ciclo con quien reportó es lo que hace que la gente siga reportando. Sólo se
        // avisa cuando el estado cambia de verdad (corregir una errata en la respuesta no debe
        // generar otra notificación) y nunca por regresar un reporte a "Nuevo".
        if (statusChanged && status != FeedbackStatus.New && report.ReporterUserId != changedByUserId)
        {
            var message = $"Tu reporte \"{Excerpt(report.Description, 80)}\" ahora está: {StatusLabels[status]}.";
            if (response is not null)
                message += $" Respuesta: {Excerpt(response, 250)}";

            notificationService.Notify(report.ReporterUserId, message, null);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Reporte {ReportId} pasó a {Status} por {ChangedBy}.", id, status, changedByUserId);
    }

    public async Task<FeedbackScreenshotFile> OpenScreenshotAsync(Guid id, CancellationToken cancellationToken)
    {
        var screenshot = await dbContext.FeedbackReports
            .AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new { f.ScreenshotStorageKey, f.ScreenshotContentType })
            .FirstOrDefaultAsync(cancellationToken);

        if (screenshot?.ScreenshotStorageKey is null || screenshot.ScreenshotContentType is null)
            throw new NotFoundException("Captura del reporte", id);

        try
        {
            var content = await fileStorage.OpenReadAsync(screenshot.ScreenshotStorageKey, cancellationToken);
            var extension = screenshot.ScreenshotContentType == "image/png" ? "png" : "jpg";
            return new FeedbackScreenshotFile(content, screenshot.ScreenshotContentType, $"reporte-{id:N}.{extension}");
        }
        catch (FileNotFoundException)
        {
            // La fila existe pero el archivo ya no (carpeta restaurada sin él, borrado manual): para
            // quien consulta es lo mismo que no tener captura, no un error del servidor.
            logger.LogWarning("La captura del reporte {ReportId} no está en el almacenamiento.", id);
            throw new NotFoundException("Captura del reporte", id);
        }
    }

    public async Task<IReadOnlyList<FeedbackMessageDto>> ListMessagesAsync(Guid reportId, CurrentUser actor, CancellationToken cancellationToken)
    {
        await EnsureCanSeeAsync(reportId, actor, cancellationToken);
        return await dbContext.FeedbackMessages.AsNoTracking()
            .Where(m => m.ReportId == reportId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new FeedbackMessageDto(m.Id, m.AuthorUserId, m.AuthorName, m.FromSupport, m.Text, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FeedbackMessageDto> AddMessageAsync(Guid reportId, CurrentUser actor, string actorName, string text, CancellationToken cancellationToken)
    {
        var report = await EnsureCanSeeAsync(reportId, actor, cancellationToken);
        var clean = text?.Trim() ?? string.Empty;
        if (clean.Length == 0)
            throw new RequestValidationException("feedback-message-empty", "Escribe el mensaje.");
        if (clean.Length > FeedbackLimits.MessageMaxLength)
            throw new RequestValidationException("feedback-message-too-long", $"El mensaje no puede pasar de {FeedbackLimits.MessageMaxLength} caracteres.");

        // Un AdminTotal que escribe en su propio reporte habla como quien reportó, no como soporte.
        var fromSupport = actor.Role == ApplicationRoles.AdminTotal && report.ReporterUserId != actor.UserId;
        var message = new FeedbackMessage
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            AuthorUserId = actor.UserId,
            AuthorName = Truncate(actorName, 200) ?? string.Empty,
            FromSupport = fromSupport,
            Text = clean,
            CreatedAt = clock.UtcNow,
        };
        dbContext.FeedbackMessages.Add(message);

        var about = $"\"{Excerpt(report.Description, 60)}\"";
        if (fromSupport)
        {
            notificationService.Notify(report.ReporterUserId,
                $"Soporte te escribió sobre tu reporte {about}: {Excerpt(clean, 200)} (ábrelo en Reportar › Mis reportes).", null);
        }
        else
        {
            var admins = await userManager.GetUsersInRoleAsync(ApplicationRoles.AdminTotal);
            notificationService.NotifyMany(
                admins.Where(a => a.Status == UserStatus.Active && a.Id != actor.UserId).Select(a => a.Id),
                $"{message.AuthorName} escribió en su reporte {about}: {Excerpt(clean, 200)}", null);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FeedbackMessageDto(message.Id, message.AuthorUserId, message.AuthorName, message.FromSupport, message.Text, message.CreatedAt);
    }

    public async Task<FeedbackExportFile> ExportAsync(FeedbackStatus? status, CancellationToken cancellationToken)
    {
        var reports = await ListAsync(status, cancellationToken);
        string[] headers = ["Fecha", "Tipo", "Estado", "Reportó", "Correo", "Rol", "Descripción", "Respuesta de soporte", "Último cambio de estado", "Mensajes", "Página"];
        var rows = reports.Select(r => (IReadOnlyList<string?>)[
            Local(r.CreatedAt), KindLabels[r.Kind], StatusLabels[r.Status], r.ReporterName, r.ReporterEmail, r.ReporterRole,
            r.Description, r.AdminResponse, r.StatusChangedAt is { } changed ? Local(changed) : null, r.MessageCount.ToString(), r.PageUrl]);
        var content = XlsxWriter.Build("Reportes", headers, rows, [17, 12, 13, 24, 28, 18, 60, 50, 20, 10, 40]);
        return new FeedbackExportFile(content, XlsxWriter.ContentType, $"reportes-{Local(clock.UtcNow, "yyyy-MM-dd")}.xlsx");
    }

    /// <summary>Quien reportó o un AdminTotal; para los demás el reporte "no existe" (no se revela).</summary>
    private async Task<FeedbackReport> EnsureCanSeeAsync(Guid reportId, CurrentUser actor, CancellationToken cancellationToken)
    {
        var report = await dbContext.FeedbackReports.AsNoTracking().FirstOrDefaultAsync(f => f.Id == reportId, cancellationToken);
        if (report is null || (report.ReporterUserId != actor.UserId && actor.Role != ApplicationRoles.AdminTotal))
            throw new NotFoundException("Reporte", reportId);
        return report;
    }

    // Hora de México (UTC-6 fijo, igual que MexicoBusinessCalendar).
    private static string Local(DateTimeOffset at, string format = "dd/MM/yyyy HH:mm") =>
        at.ToOffset(TimeSpan.FromHours(-6)).ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private async Task<(string StorageKey, string ContentType)> StoreScreenshotAsync(FeedbackScreenshot screenshot, CancellationToken cancellationToken)
    {
        var contentType = screenshot.ContentType.Trim().ToLowerInvariant();
        if (contentType is not ("image/png" or "image/jpeg"))
            throw new RequestValidationException("feedback-screenshot-content-type", "La captura debe ser una imagen PNG o JPG.");

        var maxSize = attachmentPolicy.MaxSizeBytes;
        var tooLarge = $"La captura excede el tamaño máximo permitido ({maxSize / (1024 * 1024)} MB).";
        if (screenshot.DeclaredSizeBytes <= 0 || screenshot.DeclaredSizeBytes > maxSize)
            throw new RequestValidationException("feedback-screenshot-too-large", tooLarge);

        // Mismo criterio que los adjuntos del NPI: se valida la firma real del archivo, nunca sólo
        // el Content-Type que declara el navegador.
        using var buffered = new MemoryStream();
        await screenshot.Content.CopyToAsync(buffered, cancellationToken);
        if (buffered.Length > maxSize)
            throw new RequestValidationException("feedback-screenshot-too-large", tooLarge);

        buffered.Position = 0;
        var header = new byte[Math.Min(8, buffered.Length)];
        _ = await buffered.ReadAsync(header, cancellationToken);
        if (!FileSignatureValidator.MatchesContentType(header, contentType))
            throw new RequestValidationException("feedback-screenshot-signature-mismatch", "El contenido de la captura no corresponde a una imagen PNG o JPG válida.");

        buffered.Position = 0;
        var stored = await fileStorage.SaveAsync(buffered, contentType == "image/png" ? "reporte.png" : "reporte.jpg", cancellationToken);
        return (stored.StorageKey, contentType);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string Excerpt(string value, int maxLength)
    {
        var singleLine = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= maxLength ? singleLine : singleLine[..(maxLength - 1)] + "…";
    }
}
