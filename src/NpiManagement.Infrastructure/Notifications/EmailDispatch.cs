using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace NpiManagement.Infrastructure.Notifications;

/// <summary>Un aviso que espera convertirse en correo. Se resuelve destinatario y contenido al enviarlo, no al encolarlo.</summary>
public sealed record PendingEmailNotification(Guid RecipientUserId, string Message, Guid? NpiId);

/// <summary>
/// Buffer por petición. Vive durante la transacción y lo vacía <see cref="Persistence.EfUnitOfWork"/>
/// justo después de que SaveChanges confirma — nunca antes.
/// </summary>
public sealed class PendingEmailNotifications
{
    private readonly List<PendingEmailNotification> _items = [];

    public void Add(PendingEmailNotification item) => _items.Add(item);

    /// <summary>Devuelve lo acumulado y deja el buffer limpio, para que un segundo SaveChanges en la misma petición no reenvíe lo mismo.</summary>
    public IReadOnlyList<PendingEmailNotification> Drain()
    {
        if (_items.Count == 0) return [];
        var copy = _items.ToArray();
        _items.Clear();
        return copy;
    }
}

public interface IEmailDispatchQueue
{
    /// <summary>No bloquea nunca: si la cola está llena, descarta y lo registra. Un pico de correo no puede frenar el workflow.</summary>
    void Enqueue(PendingEmailNotification notification);

    IAsyncEnumerable<PendingEmailNotification> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Cola en memoria entre la petición HTTP y el worker que envía. Deliberadamente **no** es una
/// tabla de outbox: con este volumen (decenas de correos al día) y con la campana de la app como
/// fuente de verdad, perder un correo porque el servidor se reinició en el segundo exacto es un
/// costo aceptable frente a la maquinaria de una outbox con reintentos. Si algún día el correo
/// pasa a ser crítico, esta interfaz es el punto por donde se sustituye.
/// </summary>
public sealed class ChannelEmailDispatchQueue(ILogger<ChannelEmailDispatchQueue> logger) : IEmailDispatchQueue
{
    // Acotada: si el servidor de correo se cae y la cola crece sin control, preferimos descartar
    // avisos a agotar la memoria del proceso y tumbar toda la API.
    private readonly Channel<PendingEmailNotification> _channel =
        Channel.CreateBounded<PendingEmailNotification>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public void Enqueue(PendingEmailNotification notification)
    {
        if (!_channel.Writer.TryWrite(notification))
            logger.LogWarning("Cola de correo llena; se descartó el aviso del NPI {NpiId}. La notificación en la app sí se guardó.", notification.NpiId);
    }

    public IAsyncEnumerable<PendingEmailNotification> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
