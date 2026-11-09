using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Sispro;

/// <summary>
/// Al arrancar con la etapa de cotización apagada (SisproQuoteGate), suelta los NPIs que se quedaron
/// esperando una cotización mientras estuvo encendida. Idempotente: si no hay nada que soltar no
/// escribe, así que puede correr en cada arranque. Un error aquí se registra y no tumba la API.
/// </summary>
public sealed class SisproQuoteWaitRelease(IServiceScopeFactory scopeFactory, ILogger<SisproQuoteWaitRelease> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (SisproQuoteGate.Enabled) return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NpiManagementDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<INpiRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var calendar = scope.ServiceProvider.GetRequiredService<IBusinessCalendar>();

            var candidates = await dbContext.Npis.AsNoTracking()
                .Where(n => n.LifecycleStatus == LifecycleStatus.Active
                    && n.FeasibilityReviews.Any(f => f.Area == AreaType.Ingenieria)
                    && !n.PrintCardVersions.Any())
                .Select(n => n.Id)
                .ToListAsync(stoppingToken);

            var released = new List<string>();
            foreach (var id in candidates)
            {
                var npi = await repository.GetByIdAsync(id, stoppingToken);
                if (npi is not null && npi.ReleaseFromSisproQuoteWait(clock, calendar)) released.Add(npi.NpiNumber.ToString());
            }

            if (released.Count == 0) return;
            await dbContext.SaveChangesAsync(stoppingToken);
            logger.LogWarning("Etapa de cotización SISPRO apagada: se soltaron {Count} NPIs que la esperaban: {Npis}", released.Count, string.Join(", ", released));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron soltar los NPIs que esperaban la cotización de SISPRO.");
        }
    }
}
