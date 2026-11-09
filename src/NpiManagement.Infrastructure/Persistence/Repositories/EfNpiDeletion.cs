using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Borra el NPI con DELETE directo en SQL: las tablas satélite (bitácora, adjuntos, versiones de
/// PrintCard, comentarios, materiales...) se van por el ON DELETE CASCADE de sus llaves. Las
/// notificaciones y las marcas de chat leído no tienen llave hacia Npis y se borran aquí. Los
/// archivos se borran después de confirmar en la base; si alguno falla sólo queda huérfano en disco.
/// </summary>
public sealed class EfNpiDeletion(NpiManagementDbContext dbContext, IFileStorage fileStorage, ILogger<EfNpiDeletion> logger) : INpiDeletion
{
    public async Task DeleteAsync(Guid npiId, string npiLabel, Guid actorUserId, string actorName, CancellationToken cancellationToken)
    {
        // Links externos no tienen archivo en el almacenamiento.
        var storageKeys = await dbContext.Npis.AsNoTracking()
            .Where(n => n.Id == npiId)
            .SelectMany(n => n.Attachments)
            .Where(a => a.Url == null)
            .Select(a => a.StorageKey)
            .ToListAsync(cancellationToken);

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            await dbContext.Notifications.Where(n => n.NpiId == npiId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.NpiChatReads.Where(r => r.NpiId == npiId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Npis.Where(n => n.Id == npiId).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogWarning("NPI {NpiNumber} ({NpiId}) eliminado por {ActorName} ({ActorId}); {Files} archivo(s) adjunto(s).",
            npiLabel, npiId, actorName, actorUserId, storageKeys.Count);

        foreach (var key in storageKeys.Where(k => !string.IsNullOrEmpty(k)))
        {
            try { await fileStorage.DeleteAsync(key, cancellationToken); }
            catch (Exception ex) { logger.LogWarning(ex, "No se pudo borrar el adjunto {StorageKey} del NPI eliminado {NpiId}.", key, npiId); }
        }
    }
}
