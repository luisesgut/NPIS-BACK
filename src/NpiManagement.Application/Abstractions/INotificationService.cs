namespace NpiManagement.Application.Abstractions;

public sealed record NotificationDto(Guid Id, string Message, Guid? NpiId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

/// <summary>
/// Por dónde sale un aviso. El valor por defecto es <see cref="Bell"/> deliberadamente: el riesgo
/// real del correo no es técnico sino el ruido — si cada cambio de estado de cada NPI genera un
/// correo, la gente filtra al remitente a una carpeta en una semana y el sistema completo deja de
/// servir. Sólo se marca <see cref="BellAndEmail"/> lo que exige que ESA persona haga algo
/// (asignaciones, "requiere cambios", "puedes registrar el PT"); el progreso informativo se queda
/// en la campana.
/// </summary>
public enum NotificationChannel
{
    /// <summary>Sólo la campana dentro de la app.</summary>
    Bell = 0,

    /// <summary>Campana y además correo — reservado a lo accionable.</summary>
    BellAndEmail = 1,
}

/// <summary>
/// Notificaciones en-app, de sólo consulta (sin push en tiempo real — ver ADR-018).
/// Notify/NotifyMany sólo agregan al DbContext (no llaman SaveChanges): el handler de la acción de
/// workflow que las origina confirma todo en una sola transacción junto con la mutación del Npi,
/// el TimerEvent y el AuditEvent (docs/domain-model.md §6).
///
/// Los avisos marcados <see cref="NotificationChannel.BellAndEmail"/> se encolan durante la
/// transacción y se envían **después** de que ésta confirma (ver EfUnitOfWork): si se enviaran al
/// vuelo, un rollback dejaría a alguien con un correo diciéndole que se le asignó un NPI que en
/// realidad nunca se le asignó.
/// </summary>
public interface INotificationService
{
    void Notify(Guid recipientUserId, string message, Guid? npiId, NotificationChannel channel = NotificationChannel.Bell);

    void NotifyMany(IEnumerable<Guid> recipientUserIds, string message, Guid? npiId, NotificationChannel channel = NotificationChannel.Bell);

    /// <summary>
    /// Aviso de campana que se agrupa por <paramref name="groupKey"/>: si el destinatario ya tiene
    /// uno sin leer con esa clave, se actualiza (texto y fecha) en lugar de agregar otro. Así 15
    /// mensajes de chat seguidos son una sola notificación, no 15. Nunca sale por correo.
    /// </summary>
    Task NotifyGroupedAsync(Guid recipientUserId, string groupKey, string message, Guid? npiId, CancellationToken cancellationToken);

    /// <summary>Marca como leídos los avisos sin leer de ese grupo (p. ej. al abrir el chat). Lo confirma IUnitOfWork.</summary>
    Task MarkGroupReadAsync(Guid recipientUserId, string groupKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid recipientUserId, bool unreadOnly, CancellationToken cancellationToken);

    Task MarkReadAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken);
}
