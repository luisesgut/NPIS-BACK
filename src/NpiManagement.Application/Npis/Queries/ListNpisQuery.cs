using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Queries;

public sealed record ListNpisQuery(NpiListFilter Filter, CurrentUser Actor);

public sealed class ListNpisQueryHandler(INpiListReader reader, NpiSituationResolver situations)
{
    public async Task<PagedResult<NpiListItemDto>> HandleAsync(ListNpisQuery query, CancellationToken cancellationToken)
    {
        var accessPredicate = NpiAuthorization.BuildAccessPredicate(query.Actor);
        var filter = query.Filter with { OnlyIds = null };
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

    private static string? Known(string? value, IReadOnlyList<string> allowed) =>
        allowed.FirstOrDefault(v => string.Equals(v, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
