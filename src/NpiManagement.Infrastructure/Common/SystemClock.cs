using NpiManagement.Domain.Common;

namespace NpiManagement.Infrastructure.Common;

/// <summary>
/// Implementación de IClock respaldada por el reloj real del sistema. Vive en Infrastructure
/// (no en Domain) porque depender del reloj físico de la máquina es una preocupación de
/// entorno/ejecución, no una regla de negocio — el dominio sólo conoce la abstracción IClock.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
