using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Npis.SapOrders;
using NpiManagement.Domain;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// NPIs que esperan la orden de venta de Comercial (2026-11-27), en una sola consulta. "Cambio del cliente en
/// revisión" se calcula igual que Npi.ClientChangeUnderReview (y que NpiTurns): mientras dure, no hay pedido.
/// </summary>
public sealed class EfPendingSapOrderReader(NpiManagementDbContext dbContext) : IPendingSapOrderReader
{
    public async Task<IReadOnlyList<PendingSapOrderRow>> ListAsync(IReadOnlyCollection<Guid> commercialUserIds, CancellationToken cancellationToken)
    {
        if (commercialUserIds.Count == 0) return [];
        var owners = commercialUserIds.Distinct().ToList();

        var rows = await dbContext.Npis.AsNoTracking()
            .Where(n => n.LifecycleStatus == LifecycleStatus.Active && n.CodigoPT != null && n.PedidoSapRegisteredAt == null
                && owners.Contains(n.AssignedCommercialUserId))
            .Where(n => !n.ClosureProposals.Any(p => p.Status == ClosureProposalStatus.Pending && p.Area == AreaType.Comercial))
            .Select(n => new
            {
                n.Id,
                n.NpiNumber,
                n.Capture.Cliente,
                n.Capture.DescripcionProducto,
                n.CodigoPT,
                n.PtName,
                n.PtRegisteredAt,
                n.SapOrderDeferredUntil,
                n.SapOrderDeferralReason,
                n.AssignedCommercialUserId,
                PrintCardLoaded = n.PrintCardLoadedAt != null,
                PendingReconfirmation = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.Reconfirmation && c.ResolvedAt == null),
                PendingCommercialEdit = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.CommercialEdit && c.ResolvedAt == null),
                PendingDesignReconfirmation = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.DesignReconfirmation && c.ResolvedAt == null),
            })
            .ToListAsync(cancellationToken);

        var names = await dbContext.Users.AsNoTracking().Where(u => owners.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName }).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return rows
            .Where(r => !(r.PendingDesignReconfirmation || r.PendingReconfirmation || r.PendingCommercialEdit || !r.PrintCardLoaded))
            .Select(r => new PendingSapOrderRow(
                r.Id, r.NpiNumber?.Value, r.Cliente, r.DescripcionProducto, r.CodigoPT!, r.PtName, r.PtRegisteredAt,
                r.SapOrderDeferredUntil, r.SapOrderDeferralReason, r.AssignedCommercialUserId,
                names.TryGetValue(r.AssignedCommercialUserId, out var name) ? name : null))
            .ToList();
    }
}
