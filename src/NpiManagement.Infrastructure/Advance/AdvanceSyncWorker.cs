using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis.Advance;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Advance;

/// <summary>
/// Cada N minutos revisa contra Advance los NPIs que tienen PrintCard (activos, y los completados
/// recientes por si cambia el final) y aplica lo que corresponda (AdvanceSyncService). Un NPI con error
/// no detiene a los demás; nada de esto tumba la API.
/// </summary>
public sealed class AdvanceSyncWorker(IServiceScopeFactory scopeFactory, IOptions<AdvanceOptions> options, ILogger<AdvanceSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Habilitado) return;
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.Value.MinutosEntreRevisiones, 1, 240));
        // Un respiro al arrancar para no competir con el resto del arranque de la API.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Falló la revisión de PrintCards contra Advance."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        List<Guid> candidates;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NpiManagementDbContext>();
            var since = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, options.Value.DiasCompletadosRevisados));
            // Sólo los NPIs nuevos (creados desde AplicarDesde): los anteriores se quedan como están.
            var from = options.Value.AplicarDesde ?? DateTimeOffset.MinValue;
            candidates = await db.Npis.AsNoTracking()
                .Where(n => n.CreatedAt >= from)
                .Where(n => n.PrintCardVersions.Any(v => v.Approvals.Any(a => a.Role == ApproverRole.Client))
                    && (n.LifecycleStatus == LifecycleStatus.Active
                        || (n.LifecycleStatus == LifecycleStatus.Completed && n.PrintCardLoadedAt != null && n.PrintCardLoadedAt > since)))
                .Select(n => n.Id)
                .ToListAsync(cancellationToken);
        }

        var touched = 0;
        foreach (var id in candidates)
        {
            // Un scope por NPI: si uno falla (conflicto de concurrencia, Advance caído), los demás siguen.
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<INpiRepository>();
                var sync = scope.ServiceProvider.GetRequiredService<AdvanceSyncService>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var npi = await repository.GetByIdAsync(id, cancellationToken);
                if (npi is null) continue;
                var result = await sync.SyncAsync(npi, cancellationToken);
                if (result.Changes.Count == 0) continue;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                touched++;
                logger.LogInformation("Advance → {Npi}: {Changes}", npi.NpiNumber?.Value, string.Join(", ", result.Changes));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "No se pudo sincronizar con Advance el NPI {Id}.", id); }
        }
        if (touched > 0) logger.LogInformation("Advance: {Count} NPIs actualizados de {Total} revisados.", touched, candidates.Count);
    }
}
