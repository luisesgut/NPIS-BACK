using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Application.Npis.Queries;

public sealed record ListNpisQuery(NpiListFilter Filter, CurrentUser Actor);

public sealed class ListNpisQueryHandler(INpiListReader reader)
{
    public Task<PagedResult<NpiListItemDto>> HandleAsync(ListNpisQuery query, CancellationToken cancellationToken)
    {
        var accessPredicate = NpiAuthorization.BuildAccessPredicate(query.Actor);
        return reader.ListAsync(query.Filter, accessPredicate, cancellationToken);
    }
}
