namespace NpiManagement.Domain.Common;

/// <summary>
/// Abstracción del reloj. El dominio nunca llama a DateTime.UtcNow/DateTimeOffset.UtcNow
/// directamente, para que los cronómetros y la aritmética de horario hábil sean deterministas
/// y comprobables en pruebas (ver docs/domain-model.md, VO TimerState).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
