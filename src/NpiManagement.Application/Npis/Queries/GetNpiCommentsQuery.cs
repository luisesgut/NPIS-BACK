using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Application.Npis.Queries;

public sealed record GetNpiCommentsQuery(Guid NpiId, CurrentUser Actor, int Page = 1, int PageSize = 20);

public sealed class GetNpiCommentsQueryHandler(INpiRepository repository)
{
    public async Task<PagedResult<CommentDto>> HandleAsync(GetNpiCommentsQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(query.Actor, ownership)) throw new NotFoundException("Npi", query.NpiId);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var ordered = npi.Comments.OrderByDescending(c => c.CreatedAt).ToList();
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommentDto(c.Id, c.AuthorUserId, c.AuthorName, c.Text, c.CreatedAt))
            .ToList();

        return new PagedResult<CommentDto>(items, page, pageSize, ordered.Count);
    }
}
