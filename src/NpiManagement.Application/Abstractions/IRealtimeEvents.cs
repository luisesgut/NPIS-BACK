namespace NpiManagement.Application.Abstractions;

/// <summary>NPI que cambió y quiénes participan en él (antes y después del cambio, por si lo reasignaron).</summary>
public sealed record NpiChange(Guid NpiId, IReadOnlyCollection<Guid> ParticipantIds);

/// <summary>
/// Avisos en tiempo real para que las pantallas abiertas se actualicen sin F5. Sólo dicen "qué
/// cambió"; cada cliente vuelve a pedir los datos por la API, con su propia autorización. Se publican
/// después de guardar (ver EfUnitOfWork): nunca se avisa de algo que terminó en rollback.
/// </summary>
public interface IRealtimeEvents
{
    /// <summary>A los participantes de cada NPI y a los administradores (ven todos los NPIs).</summary>
    Task NpisChangedAsync(IReadOnlyCollection<NpiChange> changes);

    /// <summary>A quienes les llegó (o leyeron) una notificación: refresca su campana.</summary>
    Task NotificationsChangedAsync(IReadOnlyCollection<Guid> userIds);
}

/// <summary>Sin tiempo real (pruebas, herramientas): las pantallas se actualizan con sus consultas normales.</summary>
public sealed class NullRealtimeEvents : IRealtimeEvents
{
    public Task NpisChangedAsync(IReadOnlyCollection<NpiChange> changes) => Task.CompletedTask;
    public Task NotificationsChangedAsync(IReadOnlyCollection<Guid> userIds) => Task.CompletedTask;
}
