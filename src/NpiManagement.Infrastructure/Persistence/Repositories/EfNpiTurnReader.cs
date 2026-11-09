using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Junta en una sola consulta lo que NpiTurns necesita de cada NPI activo o borrador (banderas como
/// EXISTS en SQL, sin cargar colecciones) y calcula los turnos en memoria.
/// </summary>
public sealed class EfNpiTurnReader(NpiManagementDbContext dbContext, IClock clock, IBusinessCalendar calendar) : INpiTurnReader
{
    public async Task<IReadOnlyList<TurnItemDto>> ListAsync(Expression<Func<Npi, bool>> accessPredicate, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Npis.AsNoTracking()
            .Where(accessPredicate)
            .Where(n => n.LifecycleStatus == LifecycleStatus.Active || n.LifecycleStatus == LifecycleStatus.Draft)
            .Select(n => new
            {
                n.Id,
                n.NpiNumber,
                n.LifecycleStatus,
                n.Capture.Cliente,
                n.Capture.DescripcionProducto,
                n.AssignedCommercialUserId,
                n.AssignedEngineeringUserId,
                n.AssignedDesignUserId,
                n.CommercialTimer,
                n.EngineeringTimer,
                n.DesignTimer,
                EngineeringFeasibility = n.FeasibilityReviews.Any(f => f.Area == AreaType.Ingenieria),
                DesignFeasibility = n.FeasibilityReviews.Any(f => f.Area == AreaType.Diseno),
                ArtesValidadas = n.ArtesValidadasAt != null,
                PendingEngineeringChanges = n.ChangeRequests.Any(c => c.FromArea == AreaType.Ingenieria && c.ToArea == AreaType.Comercial && c.ResolvedAt == null),
                PendingDesignChanges = n.ChangeRequests.Any(c => c.FromArea == AreaType.Diseno && c.ToArea == AreaType.Comercial && c.Origin == ChangeRequestOrigin.Internal && c.ResolvedAt == null),
                PendingArtworkRejection = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.ArtworkRejected && c.ResolvedAt == null),
                LatestOpenClientChangeAt = n.ChangeRequests.Where(c => c.Origin == ChangeRequestOrigin.ClientReported && c.ResolvedAt == null).Max(c => (DateTimeOffset?)c.RequestedAt),
                PendingCommercialInfo = n.InformationRequests.Any(r => r.Type == InformationRequestType.Commercial && r.Status == InformationRequestStatus.Pending),
                PendingEngineeringInfo = n.InformationRequests.Any(r => r.Type == InformationRequestType.Engineering && r.Status == InformationRequestStatus.Pending),
                SuppliesTemporaryRejection = n.SuppliesTemporaryRejection != null,
                Current = n.PrintCardVersions.OrderByDescending(v => v.VersionNumber).Select(v => new
                {
                    v.UploadedAt,
                    Rejected = v.RejectedAt != null,
                    EngineeringApproved = v.Approvals.Any(a => a.Role == ApproverRole.Engineering),
                    ClientApproved = v.Approvals.Any(a => a.Role == ApproverRole.Client),
                }).FirstOrDefault(),
                PrintCardSentToClient = n.PrintCardSentToClientAt != null,
                PrintCardLoaded = n.PrintCardLoadedAt != null,
                PtRegistered = n.PtRegisteredAt != null,
                SapOrderRegistered = n.PedidoSapRegisteredAt != null,
                EtCreated = n.EtCreatedAt != null,
                EtLoaded = n.EtLoadedInSisproAt != null,
                SisproQuoteAccepted = n.SisproQuotes.Any(q => q.Status == SisproQuoteStatus.Accepted),
                PendingReconfirmation = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.Reconfirmation && c.ResolvedAt == null),
                PendingCommercialEdit = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.CommercialEdit && c.ResolvedAt == null),
            })
            .ToListAsync(cancellationToken);

        var userIds = rows.SelectMany(r => new[] { (Guid?)r.AssignedCommercialUserId, r.AssignedEngineeringUserId, r.AssignedDesignUserId })
            .OfType<Guid>().Distinct().ToList();
        var names = await dbContext.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName }).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        string? NameOf(Guid? id) => id is { } value && names.TryGetValue(value, out var name) ? name : null;

        var now = clock.UtcNow;
        var items = new List<TurnItemDto>();
        foreach (var r in rows)
        {
            // Igual que Npi.PendingClientChangeRequest: los cambios del cliente anteriores a la versión vigente ya los atendió esa versión.
            var pendingClientChanges = r.LatestOpenClientChangeAt is { } changeAt && (r.Current is null || changeAt >= r.Current.UploadedAt);
            var facts = new TurnFacts(
                r.LifecycleStatus, r.AssignedEngineeringUserId != null, r.AssignedDesignUserId != null,
                r.EngineeringFeasibility, r.DesignFeasibility, r.ArtesValidadas,
                r.PendingEngineeringChanges, r.PendingDesignChanges, r.PendingArtworkRejection, pendingClientChanges,
                r.PendingCommercialInfo, r.SuppliesTemporaryRejection,
                r.Current is not null, r.Current?.Rejected ?? false, r.Current?.EngineeringApproved ?? false, r.Current?.ClientApproved ?? false,
                r.PrintCardSentToClient, r.PrintCardLoaded, r.PtRegistered, r.SapOrderRegistered, r.EtCreated, r.EtLoaded,
                SisproQuoteGate.Enabled && r.LifecycleStatus == LifecycleStatus.Active && r.EngineeringFeasibility && !r.SisproQuoteAccepted && r.Current is null,
                r.PendingReconfirmation,
                r.PendingCommercialEdit,
                r.PendingEngineeringInfo);

            foreach (var turn in NpiTurns.For(facts))
            {
                var (timer, assignee) = turn.Area switch
                {
                    AreaType.Ingenieria => (r.EngineeringTimer, r.AssignedEngineeringUserId),
                    AreaType.Diseno => (r.DesignTimer, r.AssignedDesignUserId),
                    _ => (r.CommercialTimer, (Guid?)r.AssignedCommercialUserId),
                };
                DateTimeOffset? since = timer.IsRunning ? timer.RunningSinceAt : null;
                items.Add(new TurnItemDto(
                    r.Id, r.NpiNumber?.Value, r.Cliente, r.DescripcionProducto, turn.Area.ToString(), turn.Action,
                    since, since is { } s ? calendar.BusinessMillisecondsBetween(s, now) : null, NameOf(assignee)));
            }
        }
        return items;
    }
}
