using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace NpiManagement.Infrastructure.FileStorage;

/// <summary>Confirma que la raíz de almacenamiento de adjuntos existe y admite escritura — sin esto, /api/npis/{id}/attachments fallaría en cada intento.</summary>
public sealed class FileStorageHealthCheck(IOptions<FileStorageOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var rootPath = Path.GetFullPath(options.Value.RootPath);
            Directory.CreateDirectory(rootPath);

            var probeFile = Path.Combine(rootPath, $".health-{Guid.NewGuid():N}");
            File.WriteAllText(probeFile, string.Empty);
            File.Delete(probeFile);

            return Task.FromResult(HealthCheckResult.Healthy($"Almacenamiento escribible en '{rootPath}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("No se pudo escribir en la raíz de almacenamiento de adjuntos.", ex));
        }
    }
}
