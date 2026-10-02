using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lectura de listado paginada — sólo trae columnas de resumen (nunca las colecciones satélite
/// completas de cada NPI) y filtra con WHERE en SQL, no en memoria (authorization-matrix.md §0).
/// Los 4 cronómetros sí se incluyen (Fase 8): son columnas propias del Npi, no colecciones.
/// </summary>
public sealed class EfNpiListReader(NpiManagementDbContext dbContext, IClock clock, IBusinessCalendar calendar) : INpiListReader
{
    public async Task<PagedResult<NpiListItemDto>> ListAsync(NpiListFilter filter, Expression<Func<Npi, bool>> accessPredicate, CancellationToken cancellationToken)
    {
        var query = dbContext.Npis.AsNoTracking().Where(accessPredicate);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(n =>
                EF.Functions.Like(n.Capture.Cliente, $"%{term}%") ||
                EF.Functions.Like(n.Capture.NumeroCotizacion, $"%{term}%") ||
                EF.Functions.Like(n.Capture.DescripcionProducto, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.LifecycleStatus) && Enum.TryParse<LifecycleStatus>(filter.LifecycleStatus, true, out var status))
            query = query.Where(n => n.LifecycleStatus == status);

        if (!string.IsNullOrWhiteSpace(filter.Priority) && Enum.TryParse<Priority>(filter.Priority, true, out var priority))
            query = query.Where(n => n.Priority == priority);

        if (filter.CreatedByUserId is { } createdBy)
            query = query.Where(n => n.CreatedByUserId == createdBy);

        if (filter.AssignedToUserId is { } assignedTo)
            query = query.Where(n =>
                n.AssignedCommercialUserId == assignedTo ||
                n.AssignedEngineeringUserId == assignedTo ||
                n.AssignedDesignUserId == assignedTo);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.NormalizedPage;
        var pageSize = filter.NormalizedPageSize;

        // Proyección intermedia: sólo columnas simples/valores propios, traducibles a SQL.
        // El .Value de NpiNumber y los .ToString() de enum se resuelven después, en memoria,
        // porque EF no puede traducir el acceso a miembros de un tipo con value converter.
        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.NpiNumber,
                n.Priority,
                n.LifecycleStatus,
                n.EngineeringAreaStatus,
                n.DesignAreaStatus,
                n.Capture.Cliente,
                n.Capture.NumeroCotizacion,
                n.Capture.DescripcionProducto,
                n.RequestDate,
                n.CreatedByUserId,
                n.AssignedEngineeringUserId,
                n.AssignedDesignUserId,
                n.AssignedCommercialUserId,
                n.GlobalTimer,
                n.CommercialTimer,
                n.EngineeringTimer,
                n.DesignTimer,
                // Hitos de la ruta: los que viven en colecciones se resuelven como EXISTS en la
                // misma consulta, sin cargar factibilidades ni versiones de PrintCard por fila.
                EngineeringFeasibilityApproved = n.FeasibilityReviews.Any(f => f.Area == AreaType.Ingenieria),
                DesignFeasibilityApproved = n.FeasibilityReviews.Any(f => f.Area == AreaType.Diseno),
                ClientApprovedPrintCard = n.PrintCardVersions.Any(v => v.Approvals.Any(a => a.Role == ApproverRole.Client)),
                PrintCardSentToClient = n.PrintCardSentToClientAt != null,
                PrintCardLoaded = n.PrintCardLoadedAt != null,
                PtRegistered = n.PtRegisteredAt != null,
                SapOrderRegistered = n.PedidoSapNumber != null,
                EtLoadedInSispro = n.EtLoadedInSisproAt != null,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new NpiListItemDto(
                r.Id, r.NpiNumber?.Value, r.Priority.ToString(), r.LifecycleStatus.ToString(),
                r.EngineeringAreaStatus.ToString(), r.DesignAreaStatus.ToString(),
                r.Cliente, r.NumeroCotizacion, r.DescripcionProducto, r.RequestDate,
                r.CreatedByUserId, r.AssignedEngineeringUserId, r.AssignedDesignUserId, r.AssignedCommercialUserId,
                NpiMapper.ToTimerDto(r.GlobalTimer, clock, calendar), NpiMapper.ToTimerDto(r.CommercialTimer, clock, calendar),
                NpiMapper.ToTimerDto(r.EngineeringTimer, clock, calendar), NpiMapper.ToTimerDto(r.DesignTimer, clock, calendar),
                new NpiStageMilestones(
                    r.EngineeringFeasibilityApproved, r.DesignFeasibilityApproved, r.PrintCardSentToClient, r.ClientApprovedPrintCard,
                    r.PrintCardLoaded, r.PtRegistered, r.SapOrderRegistered, r.EtLoadedInSispro).CompletedStages))
            .ToList();

        return new PagedResult<NpiListItemDto>(items, page, pageSize, totalCount);
    }
}
