namespace NpiManagement.Infrastructure.Sispro;

/// <summary>
/// Conexión de solo lectura a la API interna de SISPRO (cotizador oficial; autorizada por TI el
/// 2026-10-27). Usuario y Password los captura TI directamente en el servidor (web.config o
/// variables de entorno Sispro__Usuario / Sispro__Password) — nunca en el repositorio. Sin
/// usuario o contraseña la integración queda apagada y el NPI se captura a mano.
/// </summary>
public sealed class SisproOptions
{
    public const string SectionName = "Sispro";

    public string BaseUrl { get; set; } = "http://172.16.10.5:4010";
    public string? Usuario { get; set; }
    public string? Password { get; set; }
    /// <summary>El propio front de SISPRO usa 15 s; más que eso, el vendedor prefiere capturar a mano.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Enciende la etapa "Cotización SISPRO": el PrintCard espera a que el cliente acepte el precio.
    /// Apagada por omisión hasta capacitar a Comercial (SisproQuoteGate); ligar la cotización sigue
    /// disponible como opcional.
    /// </summary>
    public bool CotizacionObligatoria { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Password);
}
