using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NpiManagement.Infrastructure.Persistence;

/// <summary>Reemplaza el AddDbContextCheck estándar (basado en CanConnectAsync, que atrapa
/// cualquier excepción de conexión y la descarta) — abre la conexión directamente para que el
/// mensaje real de SqlException llegue al reporte de salud en vez de perderse.</summary>
public sealed class SqlServerHealthCheck(NpiManagementDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            await dbContext.Database.CloseConnectionAsync();
            return HealthCheckResult.Healthy("Conexión a SQL Server exitosa.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("No se pudo conectar a SQL Server.", ex);
        }
    }
}
