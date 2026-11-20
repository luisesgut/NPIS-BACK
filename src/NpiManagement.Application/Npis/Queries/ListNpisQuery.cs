using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Application.Users;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Queries;

/// <param name="Mine">Sólo los NPIs de Comercial de quien consulta (el admin de Comercial lo usa para no saturarse).</param>
/// <param name="Team">"Mi equipo" (2026-11-18): el director ve lo de sus vendedores y lo suyo; el vendedor, lo de su director.</param>
/// <param name="CopySources">"Partir de un NPI anterior" (2026-11-20): los que capturó quien consulta y, si es vendedor con
/// director, también los de su director (enviados; sus borradores no los ve).</param>
public sealed record ListNpisQuery(NpiListFilter Filter, CurrentUser Actor, bool Mine = false, bool Team = false, bool CopySources = false);

public sealed class ListNpisQueryHandler(INpiListReader reader, NpiSituationResolver situations, ICommercialTeamStore teams)
{
    public async Task<PagedResult<NpiListItemDto>> HandleAsync(ListNpisQuery query, CancellationToken cancellationToken)
    {
        // Incluye los NPIs de los clientes que le compartieron (sólo consulta).
        var accessPredicate = NpiAuthorization.BuildViewPredicate(query.Actor);
        // El área de "Mis NPIs" es la del panel (AdminTotal elige) o, si no viene, la de quien consulta.
        var mineArea = Enum.TryParse<AreaType>(query.Filter.Area, true, out var panelArea) ? panelArea : query.Actor.Area;
        var filter = query.Filter with
        {
            OnlyIds = null,
            MineUserId = query.Mine ? query.Actor.UserId : null,
            MineArea = query.Mine ? mineArea : null,
            TeamUserIds = query.Team ? await TeamUserIdsAsync(query.Actor, cancellationToken) : null,
            CreatedByAnyOf = query.CopySources
                ? (query.Actor.DirectorUserId is { } director ? [query.Actor.UserId, director] : [query.Actor.UserId])
                : null,
        };
        var situation = Known(filter.Situation, NpiSituation.All);
        var stage = Known(filter.Stage, NpiStageGroup.All);
        if (situation is not null || stage is not null)
        {
            // La situación sale de los turnos (se calculan en memoria): se resuelve primero a ids.
            var area = Enum.TryParse<AreaType>(filter.Area, true, out var a) ? a : (AreaType?)null;
            var flags = await situations.ResolveAsync(query.Actor, area, cancellationToken);
            filter = filter with
            {
                OnlyIds = flags
                    .Where(f => (situation is null || f.Value.Situations.Contains(situation)) && (stage is null || f.Value.Stages.Contains(stage)))
                    .Select(f => f.Key)
                    .ToList(),
            };
        }
        return await reader.ListAsync(filter, accessPredicate, cancellationToken);
    }

    /// <summary>De quién son los NPIs de "Mi equipo": el director y sus vendedores, o el director del vendedor.</summary>
    private async Task<IReadOnlyCollection<Guid>> TeamUserIdsAsync(CurrentUser actor, CancellationToken cancellationToken)
    {
        var members = await teams.ListMembersAsync(actor.UserId, cancellationToken);
        if (members.Count > 0) return [actor.UserId, .. members.Select(m => m.Id)];
        return actor.DirectorUserId is { } director ? [director] : [];
    }

    private static string? Known(string? value, IReadOnlyList<string> allowed) =>
        allowed.FirstOrDefault(v => string.Equals(v, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
