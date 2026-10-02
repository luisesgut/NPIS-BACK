using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Email;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Notifications;

/// <summary>
/// Consume la cola de avisos y los convierte en correos, fuera del ciclo de la petición HTTP.
///
/// Que esto viva en un servicio en segundo plano es lo que permite que un servidor de correo lento
/// o caído no retrase ni haga fallar la acción del NPI: para cuando el aviso llega aquí, la
/// transacción ya se confirmó y quien movió el NPI ya recibió su respuesta.
/// </summary>
public sealed class EmailDispatchWorker(
    IEmailDispatchQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<MailOptions> options,
    ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    private readonly MailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Envío de correo deshabilitado (Mail:Enabled=false). Las notificaciones sólo aparecerán en la app.");
            return;
        }

        logger.LogInformation("Worker de correo iniciado. Servidor: {Host}:{Port}, remitente: {From}", _options.Host, _options.Port, _options.FromAddress);

        await foreach (var pending in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(pending, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un aviso que falla no puede matar el worker: si esta excepción escapara, se
                // dejarían de enviar TODOS los correos siguientes hasta reiniciar la aplicación.
                logger.LogError(ex, "Error procesando un aviso por correo del NPI {NpiId}.", pending.NpiId);
            }
        }
    }

    private async Task ProcessAsync(PendingEmailNotification pending, CancellationToken cancellationToken)
    {
        // Scope propio: el del request que originó el aviso ya se liberó hace rato.
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiManagementDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var recipient = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == pending.RecipientUserId)
            .Select(u => new { u.Email, u.FullName, u.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
        {
            logger.LogWarning("Aviso descartado: el usuario {UserId} no existe o no tiene correo.", pending.RecipientUserId);
            return;
        }

        // No se le escribe a quien ya no opera el sistema: una cuenta dada de baja o pendiente de
        // aprobación no debería seguir recibiendo movimientos de NPIs.
        if (recipient.Status != UserStatus.Active)
        {
            logger.LogInformation("Aviso omitido: la cuenta {UserId} está en estado {Status}.", pending.RecipientUserId, recipient.Status);
            return;
        }

        var context = pending.NpiId is { } npiId
            ? await dbContext.Npis
                .AsNoTracking()
                .Where(n => n.Id == npiId)
                .Select(n => new NpiEmailContext(n.NpiNumber!.Value, n.Capture.Cliente, n.Capture.DescripcionProducto, n.Priority.ToString()))
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var subject = context is null
            ? $"[NPI] {pending.Message}"
            : $"[{context.NpiNumber}] {pending.Message}";

        var message = new EmailMessage(
            recipient.Email,
            recipient.FullName,
            subject,
            BuildHtmlBody(recipient.FullName, pending.Message, context, pending.NpiId),
            BuildTextBody(recipient.FullName, pending.Message, context, pending.NpiId));

        await sender.SendAsync(message, cancellationToken);
    }

    private sealed record NpiEmailContext(string NpiNumber, string Cliente, string Descripcion, string Prioridad);

    private string? LinkTo(Guid? npiId) =>
        npiId is { } id && !string.IsNullOrWhiteSpace(_options.FrontendBaseUrl)
            ? $"{_options.FrontendBaseUrl.TrimEnd('/')}/npi/{id}"
            : null;

    private string BuildTextBody(string name, string message, NpiEmailContext? context, Guid? npiId)
    {
        var lines = new List<string> { $"Hola {name},", "", message, "" };

        if (context is not null)
        {
            lines.Add($"NPI:        {context.NpiNumber}");
            lines.Add($"Cliente:    {context.Cliente}");
            lines.Add($"Producto:   {context.Descripcion}");
            lines.Add($"Prioridad:  {context.Prioridad}");
            lines.Add("");
        }

        if (LinkTo(npiId) is { } link)
        {
            lines.Add($"Abrir el NPI: {link}");
            lines.Add("");
        }

        lines.Add("-- ");
        lines.Add("Sistema de Gestión de NPIs - Bioflex. Este mensaje es automático, no lo respondas.");
        return string.Join("\n", lines);
    }

    private string BuildHtmlBody(string name, string message, NpiEmailContext? context, Guid? npiId)
    {
        // Todo lo que viene de la base (nombres, clientes, descripciones capturadas por usuarios)
        // se escapa: sin esto, una comilla o un signo de mayor-que en una descripción de producto
        // rompería el HTML del correo, y un valor malicioso podría inyectar marcado.
        string E(string value) => WebUtility.HtmlEncode(value);

        var detalle = context is null ? string.Empty : $"""
            <table cellpadding="0" cellspacing="0" style="margin:20px 0;font-size:14px;color:#333">
              <tr><td style="padding:4px 16px 4px 0;color:#777">NPI</td><td style="padding:4px 0;font-weight:600">{E(context.NpiNumber)}</td></tr>
              <tr><td style="padding:4px 16px 4px 0;color:#777">Cliente</td><td style="padding:4px 0">{E(context.Cliente)}</td></tr>
              <tr><td style="padding:4px 16px 4px 0;color:#777">Producto</td><td style="padding:4px 0">{E(context.Descripcion)}</td></tr>
              <tr><td style="padding:4px 16px 4px 0;color:#777">Prioridad</td><td style="padding:4px 0">{E(context.Prioridad)}</td></tr>
            </table>
            """;

        var boton = LinkTo(npiId) is { } link
            ? $"""<p style="margin:24px 0"><a href="{E(link)}" style="background:#D06430;color:#fff;padding:11px 22px;border-radius:8px;text-decoration:none;font-weight:600;display:inline-block">Abrir el NPI</a></p>"""
            : string.Empty;

        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;padding:24px">
              <p style="font-size:15px;color:#333">Hola {E(name)},</p>
              <p style="font-size:16px;color:#111;font-weight:600;line-height:1.45">{E(message)}</p>
              {detalle}
              {boton}
              <hr style="border:0;border-top:1px solid #e5e5e5;margin:28px 0 12px">
              <p style="font-size:12px;color:#999">Sistema de Gestión de NPIs — Bioflex. Este mensaje es automático, no lo respondas.</p>
            </div>
            """;
    }
}
