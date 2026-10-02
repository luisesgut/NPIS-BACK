namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Confirma la transacción. El DbContext de Infrastructure ES el Unit of Work — esta interfaz
/// sólo evita que Application dependa de EF Core directamente (instrucción explícita: "Unit of
/// Work mediante DbContext, sin repositorio genérico").
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
