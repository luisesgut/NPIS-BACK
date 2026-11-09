using System.Linq.Expressions;
using NpiManagement.Application.Authorization;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Turns;

/// <summary>
/// Un NPI que espera a un área ("Tu turno"). WaitingSince: desde cuándo corre el reloj de esa área
/// (null si su reloj no corre, p. ej. un borrador); WaitingBusinessMs: ese tiempo en horario hábil.
/// </summary>
public sealed record TurnItemDto(
    Guid NpiId, string? NpiNumber, string Cliente, string DescripcionProducto, string Area, string Action,
    DateTimeOffset? WaitingSince, long? WaitingBusinessMs, string? AssigneeName);

public interface INpiTurnReader
{
    /// <summary>Todos los turnos de los NPIs activos y borradores que el usuario puede ver.</summary>
    Task<IReadOnlyList<TurnItemDto>> ListAsync(Expression<Func<Npi, bool>> accessPredicate, CancellationToken cancellationToken);
}

/// <summary>
/// Qué NPIs entran en "Tu turno" de alguien. Igual que lo que puede ver, salvo el admin de Comercial
/// (2026-11-09): ve todos los NPIs, pero en "Tu turno" sólo los suyos (los que capturó o tiene
/// asignados), porque con los de todo Comercial se le llenaba de cosas que no le tocan. Los admins de
/// Ingeniería y Diseño siguen viendo toda su área: ellos asignan y reparten.
/// </summary>
public static class TurnScope
{
    public static bool IsRestricted(CurrentUser actor) => actor.Role == ApplicationRoles.AdminComercial;

    public static Expression<Func<Npi, bool>> For(CurrentUser actor)
    {
        if (!IsRestricted(actor)) return NpiAuthorization.BuildAccessPredicate(actor);
        var userId = actor.UserId;
        return npi => npi.CreatedByUserId == userId || npi.AssignedCommercialUserId == userId;
    }
}

/// <summary>
/// "Tu turno" de un área. Un usuario normal sólo ve lo suyo (la regla de acceso ya lo limita a sus
/// NPIs); el admin del área ve toda su área, incluido lo que falta asignar. AdminTotal elige el área.
/// </summary>
public sealed record GetMyTurnsQuery(CurrentUser Actor, AreaType? Area);

public sealed class GetMyTurnsQueryHandler(INpiTurnReader reader)
{
    public async Task<IReadOnlyList<TurnItemDto>> HandleAsync(GetMyTurnsQuery query, CancellationToken cancellationToken)
    {
        var area = query.Actor.Area == AreaType.Total ? query.Area : query.Actor.Area;
        var isAdmin = NpiAuthorization.SeesAllNpis(query.Actor);
        var turns = await reader.ListAsync(TurnScope.For(query.Actor), cancellationToken);
        return turns
            .Where(t => area is null || t.Area == area.ToString())
            .Where(t => isAdmin || !NpiTurns.AdminOnly.Contains(t.Action))
            .OrderByDescending(t => t.WaitingBusinessMs ?? -1)
            .ToList();
    }
}
