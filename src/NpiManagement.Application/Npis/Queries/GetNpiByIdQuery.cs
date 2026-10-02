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

        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(query.Actor, ownership))
            throw new NotFoundException("Npi", query.NpiId);

        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
