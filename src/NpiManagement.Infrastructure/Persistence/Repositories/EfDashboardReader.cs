using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Métricas del tablero de control (Fase 7) calculadas con GroupBy/Count/Average en SQL sobre el
/// subconjunto autorizado — nunca se traen agregados Npi completos a memoria para contarlos
/// (mismo principio que EfNpiListReader, ver docs/authorization-matrix.md §0).
/// </summary>
public sealed class EfDashboardReader(NpiManagementDbContext dbContext) : IDashboardReader
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(Expression<Func<Npi, bool>> accessPredicate, long slaThresholdBusinessMs, CancellationToken cancellationToken)
    {
        var query = dbContext.Npis.AsNoTracking().Where(accessPredicate);

        var lifecycleRows = await query
            .GroupBy(n => n.LifecycleStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countsByLifecycleStatus = lifecycleRows.ToDictionary(x => x.Status.ToString(), x => x.Count);
        var totalCount = lifecycleRows.Sum(x => x.Count);

        // Una sola consulta por prioridad: cerrados/en proceso con COUNT(CASE…) y promedio de los cerrados con AVG(CASE…) (AVG ignora NULL).
        var byPriority = await query
            .GroupBy(n => n.Priority)
            .Select(g => new
            {
                Priority = g.Key,
                Closed = g.Count(n => n.GlobalTimer.EndAt != null),
                InProcess = g.Count(n => n.GlobalTimer.EndAt == null),
                AvgMs = g.Average(n => n.GlobalTimer.EndAt != null ? (double?)n.GlobalTimer.AccumulatedBusinessMs : null),
                WithinSla = g.Count(n => n.GlobalTimer.EndAt != null && n.GlobalTimer.AccumulatedBusinessMs <= slaThresholdBusinessMs),
            })
            .ToListAsync(cancellationToken);

        var closedCount = byPriority.Sum(x => x.Closed);
        var inProcessCount = totalCount - closedCount;

        var closedCountByPriority = ToPriorityCountDictionary(byPriority.Select(x => (x.Priority, x.Closed)));
        var inProcessCountByPriority = ToPriorityCountDictionary(byPriority.Select(x => (x.Priority, x.InProcess)));
        var averageGlobalDurationBusinessMsByPriority = Enum.GetValues<Priority>()
            .ToDictionary(p => p.ToString(), p => byPriority.FirstOrDefault(x => x.Priority == p)?.AvgMs);

        double? averageGlobalDurationBusinessMs = closedCount > 0
            ? byPriority.Where(x => x.AvgMs != null).Sum(x => x.AvgMs!.Value * x.Closed) / closedCount
            : null;

        // Promedio por área sobre los NPIs cuyo cronómetro de esa área ya cerró (cada dashboard muestra el de su área), en una sola consulta.
        var areaAverages = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Engineering = g.Average(n => n.EngineeringTimer.EndAt != null ? (double?)n.EngineeringTimer.AccumulatedBusinessMs : null),
                Commercial = g.Average(n => n.CommercialTimer.EndAt != null ? (double?)n.CommercialTimer.AccumulatedBusinessMs : null),
                Design = g.Average(n => n.DesignTimer.EndAt != null ? (double?)n.DesignTimer.AccumulatedBusinessMs : null),
            })
            .FirstOrDefaultAsync(cancellationToken);
        var averageEngineeringDurationBusinessMs = areaAverages?.Engineering;
        var averageCommercialDurationBusinessMs = areaAverages?.Commercial;
        var averageDesignDurationBusinessMs = areaAverages?.Design;

        var withinSlaCount = byPriority.Sum(x => x.WithinSla);
        var slaCompliancePercent = closedCount > 0 ? Math.Round(100.0 * withinSlaCount / closedCount, 1) : 100.0;

        var topClients = await query
            .Where(n => n.Capture.Cliente != "")
            .GroupBy(n => n.Capture.Cliente)
            .Select(g => new { Client = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync(cancellationToken);
        var countsByClient = topClients.ToDictionary(x => x.Client, x => x.Count);

        return new DashboardSummaryDto(
            totalCount,
            countsByLifecycleStatus,
            closedCount,
            inProcessCount,
            closedCountByPriority,
            inProcessCountByPriority,
            averageGlobalDurationBusinessMs,
            averageGlobalDurationBusinessMsByPriority,
            averageEngineeringDurationBusinessMs,
            slaCompliancePercent,
            countsByClient,
            averageCommercialDurationBusinessMs,
            averageDesignDurationBusinessMs);
    }

    private static Dictionary<string, int> ToPriorityCountDictionary(IEnumerable<(Priority Priority, int Count)> rows)
    {
        var dict = Enum.GetValues<Priority>().ToDictionary(p => p.ToString(), _ => 0);
        foreach (var (priority, count) in rows) dict[priority.ToString()] = count;
        return dict;
    }
}
