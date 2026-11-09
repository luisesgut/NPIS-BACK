namespace NpiManagement.Domain;

/// <summary>
/// Interruptor de la etapa "Cotización SISPRO" (ADR-025). Encendido, el PrintCard espera a que el
/// cliente acepte el precio; apagado, el flujo es el de antes y ligar la cotización es opcional.
/// Se apaga mientras Comercial no esté capacitado en el cotizador de SISPRO (2026-10-28: se publicó
/// el back sin el front nuevo y los NPIs se quedaron esperando una cotización que nadie podía ligar).
///
/// Lo fija la API al arrancar (Sispro:CotizacionObligatoria). Las pruebas usan Override, que sólo
/// vale en su propio flujo asíncrono y no se cruza con las que corren en paralelo.
/// </summary>
public static class SisproQuoteGate
{
    private static readonly AsyncLocal<bool?> Overridden = new();

    /// <summary>Valor de la configuración. Encendido por omisión: es la regla de negocio acordada.</summary>
    public static bool Configured { get; set; } = true;

    public static bool Enabled => Overridden.Value ?? Configured;

    public static IDisposable Override(bool enabled)
    {
        var previous = Overridden.Value;
        Overridden.Value = enabled;
        return new Restore(previous);
    }

    private sealed class Restore(bool? previous) : IDisposable
    {
        public void Dispose() => Overridden.Value = previous;
    }
}
