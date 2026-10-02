namespace NpiManagement.Application.Abstractions;

/// <summary>Un correo ya renderizado, listo para entregar. Sin lógica de negocio.</summary>
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody);

/// <summary>
/// Entrega de correo. La implementación real habla SMTP (ver SmtpEmailSender), pero la interfaz no
/// menciona SMTP a propósito: el transporte de Bioflex está sujeto a decisiones de Microsoft que
/// pueden cambiar (Direct Send hoy, quizá Graph mañana), y cambiarlo debe costar una clase, no una
/// refactorización.
///
/// Nunca lanza hacia el llamador: un servidor de correo caído no puede tumbar una operación de
/// negocio que ya se confirmó en base de datos. Los fallos se registran en el log.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
