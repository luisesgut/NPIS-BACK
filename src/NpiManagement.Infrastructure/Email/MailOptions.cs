namespace NpiManagement.Infrastructure.Email;

/// <summary>
/// Configuración del envío de correo (sección <c>Mail</c>).
///
/// El despliegue de Bioflex usa **Direct Send**: se entrega directamente al MX del tenant de
/// Microsoft 365 (<c>bioflex-mx.mail.protection.outlook.com:25</c>) sin autenticación. Funciona
/// porque todos los destinatarios son del propio dominio, y el remitente va sobre el subdominio
/// <c>npi.bioflex.mx</c>, cuyo DMARC es <c>sp=none</c> — por eso no se cuarentena (verificado el
/// 2026-09-11: llega a Bandeja de entrada).
///
/// Si algún día se cambia a un buzón autenticado o a un servicio externo, basta llenar
/// <see cref="UserName"/>/<see cref="Password"/> y apuntar <see cref="Host"/>/<see cref="Port"/>
/// a donde toque: no cambia una línea de código.
/// </summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    /// <summary>Apagado por defecto: sin configuración explícita el sistema se comporta exactamente como antes de existir el correo.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;

    /// <summary>Vacíos en Direct Send. Si se llenan, se autentica.</summary>
    public string? UserName { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = "npi-noreply@npi.bioflex.mx";
    public string FromName { get; set; } = "Sistema NPI - Bioflex";

    /// <summary>
    /// Base del frontend para armar la liga al NPI. Sin esto el correo obliga a entrar y buscar el
    /// NPI a mano, que es justo lo que se quiere evitar.
    /// </summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>Un servidor de correo lento no debe retener al worker indefinidamente.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
