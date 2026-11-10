using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Application.Npis.Queries;

public sealed record GetNpiAuditQuery(Guid NpiId, CurrentUser Actor);

public sealed class GetNpiAuditQueryHandler(INpiRepository repository, IUserDirectory userDirectory)
{
    public async Task<IReadOnlyList<AuditEventDto>> HandleAsync(GetNpiAuditQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken)
            ?? throw new NotFoundException("Npi", query.NpiId);

        var ownership = NpiAuthorization.OwnershipOf(npi);
        if (!NpiAuthorization.CanView(query.Actor, ownership, npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);

        var names = await userDirectory.GetNamesAsync(npi.AuditLog.Select(e => e.ActorUserId).Distinct().ToList(), cancellationToken);
        return npi.AuditLog.OrderByDescending(e => e.OccurredAt)
            .Select(e => NpiMapper.ToAuditEventDto(e) with { ActorName = names.GetValueOrDefault(e.ActorUserId) })
            .ToList();
    }
}

public sealed record GetNpiTimerEventsQuery(Guid NpiId, CurrentUser Actor);

public sealed class GetNpiTimerEventsQueryHandler(INpiRepository repository)
{
    public async Task<IReadOnlyList<TimerEventDto>> HandleAsync(GetNpiTimerEventsQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken)
            ?? throw new NotFoundException("Npi", query.NpiId);

        var ownership = NpiAuthorization.OwnershipOf(npi);
        if (!NpiAuthorization.CanView(query.Actor, ownership, npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);

        return npi.TimerEventLog.OrderByDescending(e => e.OccurredAt).Select(NpiMapper.ToTimerEventDto).ToList();
    }
}
