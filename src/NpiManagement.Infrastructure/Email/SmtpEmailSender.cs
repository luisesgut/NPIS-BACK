using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.Email;

/// <summary>
/// Envío por SMTP con MailKit. Se usa MailKit y no <c>System.Net.Mail.SmtpClient</c> porque éste
/// está marcado obsoleto por el propio .NET y negocia STARTTLS de forma poco fiable.
/// </summary>
public sealed class SmtpEmailSender(IOptions<MailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly MailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();
        if (message.ThreadKey is { } threadKey)
        {
            // Un hilo por NPI: todos sus correos "responden" a un mensaje raíz imaginario con un
            // Message-ID fijo para ese NPI. Gmail agrupa por References/In-Reply-To; Outlook, por el
            // asunto (que es siempre el mismo para el NPI) y por Thread-Topic.
            var domain = _options.FromAddress.Contains('@') ? _options.FromAddress[(_options.FromAddress.IndexOf('@') + 1)..] : "npi.local";
            var root = $"npi-{threadKey}@{domain}";
            mime.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId(domain);
            mime.InReplyTo = root;
            mime.References.Add(root);
            mime.Headers.Add("Thread-Topic", message.Subject);
        }

        using var client = new SmtpClient { Timeout = _options.TimeoutSeconds * 1000 };

        try
        {
            // Direct Send al MX del tenant acepta texto plano y ofrece STARTTLS; StartTlsWhenAvailable
            // cubre tanto ese caso como el de un relay autenticado que exija TLS, sin ramificar.
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);

            // En Direct Send no hay credenciales: autenticar ahí provocaría un rechazo del servidor.
            if (!string.IsNullOrWhiteSpace(_options.UserName))
                await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("Correo enviado a {Recipient}: {Subject}", message.ToEmail, message.Subject);
        }
        catch (Exception ex)
        {
            // No se relanza a propósito: la acción de negocio que originó este aviso ya está
            // confirmada en base de datos y la notificación en la app ya existe. Un servidor de
            // correo caído no puede convertirse en un fallo visible para quien movió el NPI.
            logger.LogError(ex, "No se pudo enviar el correo a {Recipient} ({Subject}).", message.ToEmail, message.Subject);
        }
    }
}

/// <summary>Descarta todo. Es lo que se registra cuando <c>Mail:Enabled</c> es false — así el resto del sistema no necesita saber si hay correo o no.</summary>
public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogDebug("Correo deshabilitado (Mail:Enabled=false); se omitió el aviso a {Recipient}.", message.ToEmail);
        return Task.CompletedTask;
    }
}
