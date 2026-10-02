using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Services;

/// <summary>
/// Genera folios de NPI mediante NEXT VALUE FOR sobre el SEQUENCE nativo de SQL Server
/// (ver docs/architecture-decisions.md ADR-005). Atómico bajo concurrencia por diseño del motor
/// — no requiere bloqueo explícito ni "leer máximo + 1".
/// </summary>
public sealed class SqlServerNpiNumberGenerator(NpiManagementDbContext dbContext) : INpiNumberGenerator
{
    public async Task<NpiNumber> NextAsync(CancellationToken cancellationToken)
    {
        // SqlQueryRaw<int>(...).SingleAsync() compone la consulta cruda dentro de una subconsulta
        // para poder aplicar el LINQ "single" — y SQL Server prohíbe NEXT VALUE FOR dentro de
        // subconsultas/derived tables ("NEXT VALUE FOR function is not allowed in..."), sin
        // importar que el SQL en sí sea válido de forma aislada (reproducido contra SQL Server
        // real en Fase 8). Se ejecuta con ADO.NET directo para que el driver la mande tal cual,
        // sin que el proveedor LINQ la reescriba.
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT NEXT VALUE FOR [{NpiManagementDbContext.NpiNumberSequenceSchema}].[{NpiManagementDbContext.NpiNumberSequenceName}]";
        var currentTransaction = dbContext.Database.CurrentTransaction;
        if (currentTransaction is not null)
            command.Transaction = currentTransaction.GetDbTransaction();

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return NpiNumber.FromSequenceValue(Convert.ToInt32(result));
    }
}
