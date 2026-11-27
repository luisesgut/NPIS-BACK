using NpiManagement.Application.Authorization;
using NpiManagement.Application.Users;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Exceptions;

namespace NpiManagement.Application.Npis.SapOrders;

/// <summary>
/// Un NPI con el PT en SAP y sin orden de venta (2026-11-27). BlocksFrom: desde qué día impide crear NPIs nuevos
/// (PT + 3 días hábiles); Blocking: si ya lo impide hoy. DeferredUntil/DeferralReason: "el cliente aún no hace
/// pedido". OwnerName: el vendedor responsable cuando no es quien consulta (el director ve los de su equipo).
/// </summary>
public sealed record PendingSapOrderItemDto(
    Guid NpiId, string? NpiNumber, string Cliente, string DescripcionProducto, string CodigoPT, string? PtName,
    DateTimeOffset? PtRegisteredAt, DateOnly BlocksFrom, bool Blocking, DateOnly? DeferredUntil, string? DeferralReason,
    string? OwnerName, bool IsMine);

/// <summary>Blocked: no puede crear NPIs nuevos hasta registrar (o posponer) las órdenes de los que bloquean.</summary>
public sealed record PendingSapOrdersDto(bool Blocked, IReadOnlyList<PendingSapOrderItemDto> Items, int GraceBusinessDays, int MaxDeferralDays);

public sealed record PendingSapOrderRow(
    Guid NpiId, string? NpiNumber, string Cliente, string DescripcionProducto, string CodigoPT, string? PtName,
    DateTimeOffset? PtRegisteredAt, DateOnly? DeferredUntil, string? DeferralReason, Guid AssignedCommercialUserId, string? AssignedCommercialName);

public interface IPendingSapOrderReader
{
    /// <summary>
    /// NPIs activos con PT y sin pedido de esos responsables comerciales, cuya orden ya se puede registrar: sin un
    /// cambio del cliente en revisión (Npi.ClientChangeUnderReview) ni un cierre de Comercial esperando visto bueno.
    /// </summary>
    Task<IReadOnlyList<PendingSapOrderRow>> ListAsync(IReadOnlyCollection<Guid> commercialUserIds, CancellationToken cancellationToken);
}

/// <summary>
/// "Ponle orden de venta antes de crear otro NPI" (2026-11-27). Aplica a vendedores y directores de Comercial: el
/// vendedor, por los NPIs que tiene a su cargo; el director (admin de Comercial), por los suyos y los de sus
/// vendedores. AdminTotal y las demás áreas nunca quedan bloqueados.
/// </summary>
public sealed class PendingSapOrdersService(IPendingSapOrderReader reader, ICommercialTeamStore teams, IClock clock, IBusinessCalendar calendar)
{
    public static bool Applies(CurrentUser user) => user.Role is ApplicationRoles.UsuarioComercial or ApplicationRoles.AdminComercial;

    public async Task<PendingSapOrdersDto> GetAsync(CurrentUser actor, CancellationToken cancellationToken)
    {
        if (!Applies(actor)) return new PendingSapOrdersDto(false, [], SapOrderHold.GraceBusinessDays, SapOrderHold.MaxDeferralDays);

        var owners = new List<Guid> { actor.UserId };
        if (actor.Role == ApplicationRoles.AdminComercial)
            owners.AddRange((await teams.ListMembersAsync(actor.UserId, cancellationToken)).Select(m => m.Id));

        var rows = await reader.ListAsync(owners, cancellationToken);
        var today = SapOrderHold.LocalDate(clock.UtcNow);
        var items = rows
            .Select(r => new PendingSapOrderItemDto(
                r.NpiId, r.NpiNumber, r.Cliente, r.DescripcionProducto, r.CodigoPT, r.PtName, r.PtRegisteredAt,
                SapOrderHold.BlocksFrom(r.PtRegisteredAt, calendar),
                SapOrderHold.IsBlocking(r.PtRegisteredAt, r.DeferredUntil, today, calendar),
                r.DeferredUntil is { } until && until > today ? until : null,
                r.DeferredUntil is { } u && u > today ? r.DeferralReason : null,
                r.AssignedCommercialUserId == actor.UserId ? null : r.AssignedCommercialName,
                r.AssignedCommercialUserId == actor.UserId))
            .OrderByDescending(i => i.Blocking)
            .ThenBy(i => i.DeferredUntil is not null)
            .ThenBy(i => i.PtRegisteredAt)
            .ToList();
        return new PendingSapOrdersDto(items.Any(i => i.Blocking), items, SapOrderHold.GraceBusinessDays, SapOrderHold.MaxDeferralDays);
    }

    /// <summary>Lo usa Crear NPI: el front ya lo bloquea, pero la regla vive aquí para que no se brinque.</summary>
    public async Task EnsureCanCreateNpiAsync(CurrentUser actor, CancellationToken cancellationToken)
    {
        var pending = await GetAsync(actor, cancellationToken);
        if (!pending.Blocked) return;
        var folios = string.Join(", ", pending.Items.Where(i => i.Blocking).Select(i => i.NpiNumber ?? "sin folio"));
        throw new InvalidNpiTransitionException("sap-orders-pending",
            $"Antes de crear otro NPI, registra la orden de venta de {folios} (o indica que el cliente aún no hace pedido).");
    }
}

public sealed record GetPendingSapOrdersQuery(CurrentUser Actor);

public sealed class GetPendingSapOrdersQueryHandler(PendingSapOrdersService service)
{
    public Task<PendingSapOrdersDto> HandleAsync(GetPendingSapOrdersQuery query, CancellationToken cancellationToken) =>
        service.GetAsync(query.Actor, cancellationToken);
}
