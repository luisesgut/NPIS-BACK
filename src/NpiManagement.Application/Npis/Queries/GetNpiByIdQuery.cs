using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Queries;

public sealed record GetNpiByIdQuery(Guid NpiId, CurrentUser Actor);

public sealed class GetNpiByIdQueryHandler(INpiRepository repository, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(GetNpiByIdQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken)
            ?? throw new NotFoundException("Npi", query.NpiId);

        var ownership = NpiAuthorization.OwnershipOf(npi);
        if (!NpiAuthorization.CanView(query.Actor, ownership, npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);

        var detail = NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
        if (NpiAuthorization.IsViewOnly(query.Actor, ownership, npi.Capture.Cliente, npi.LifecycleStatus)) return detail with { ViewOnly = true };
        return NpiAuthorization.WorksOnlyByClient(query.Actor, npi) ? detail with { ClientWork = true } : detail;
    }
}
