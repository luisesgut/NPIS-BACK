namespace NpiManagement.Application.Npis.Queries;

public sealed record NpiListFilter(
    string? Search,
    string? LifecycleStatus,
    Guid? AssignedToUserId,
    Guid? CreatedByUserId,
    string? Priority,
    int Page = 1,
    int PageSize = 20)
{
    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize is < 1 or > 100 ? 20 : PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
