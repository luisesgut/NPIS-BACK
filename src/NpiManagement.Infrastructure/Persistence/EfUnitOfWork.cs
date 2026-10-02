using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure.Notifications;

namespace NpiManagement.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    NpiManagementDbContext dbContext,
    PendingEmailNotifications pendingEmails,
    IEmailDispatchQueue emailQueue) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var result = await dbContext.SaveChangesAsync(cancellationToken);

        // Éste es el único lugar del sistema donde consta que la acción de negocio realmente
        // ocurrió. Encolar aquí —y no en INotificationService.Notify— es lo que impide mandarle a
        // alguien un "se te asignó el NPI-1042" de una transacción que terminó en rollback.
        // Si SaveChanges lanza, esta línea no se alcanza y los avisos se descartan con el scope.
        foreach (var pending in pendingEmails.Drain())
            emailQueue.Enqueue(pending);

        return result;
    }
}
