namespace NpiManagement.Domain.Common;

/// <summary>
/// Marca un evento de dominio. Se usan sólo donde aportan valor real (efectos que
/// Application necesita reaccionar sin acoplar Domain a notificaciones/infraestructura),
/// no como ceremonia para cada acción — ver docs/architecture-decisions.md.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
