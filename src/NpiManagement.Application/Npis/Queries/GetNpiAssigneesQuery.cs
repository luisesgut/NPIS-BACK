using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.Npis.Queries;

public sealed record NpiAssigneeDto(Guid UserId, string FullName);

/// <summary>Responsables actuales del NPI. Comercial siempre existe; Ingeniería y Diseño pueden no estar asignados todavía.</summary>
/// <param name="ClientWorkers">Quienes trabajan el NPI por "Ver y trabajar" del cliente (2026-11-10); se administran en Usuarios.</param>
public sealed record NpiAssigneesDto(NpiAssigneeDto Commercial, NpiAssigneeDto? Engineering, NpiAssigneeDto? Design, IReadOnlyList<NpiTeamMemberDto>? Team = null, IReadOnlyList<NpiTeamMemberDto>? ClientWorkers = null);

/// <summary>Alguien del equipo del NPI (2026-11-10), con su nombre.</summary>
public sealed record NpiTeamMemberDto(Guid UserId, string FullName, string Area);

public sealed record GetNpiAssigneesQuery(Guid NpiId, CurrentUser Actor);

/// <summary>
/// Nombres de los responsables de un NPI, con la misma autorización que su detalle: quien puede
/// ver el NPI puede saber quién lo tiene. Existe aparte de /api/users porque ese listado está
/// reservado a administradores (y cada admin de área sólo ve la suya), así que los demás roles no
/// tenían cómo convertir en nombres los ids del detalle y la pantalla mostraba el id.
/// </summary>
public sealed class GetNpiAssigneesQueryHandler(INpiRepository repository, IUserDirectory userDirectory, Users.IClientViewGrantStore? clientGrants = null)
{
    /// <summary>Se muestra si la cuenta ya no existe, en lugar de exponer el id.</summary>
    public const string UnknownUserName = "Usuario no disponible";

    public async Task<NpiAssigneesDto> HandleAsync(GetNpiAssigneesQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken)
            ?? throw new NotFoundException("Npi", query.NpiId);

        var ownership = NpiAuthorization.OwnershipOf(npi);
        if (!NpiAuthorization.CanView(query.Actor, ownership, npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);

        Guid?[] assignedIds = [npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId];
        var names = await userDirectory.GetNamesAsync(
            assignedIds.OfType<Guid>().Concat(npi.Participants.Select(p => p.UserId)).Distinct().ToList(),
            cancellationToken);

        NpiAssigneeDto? ToAssignee(Guid? userId) =>
            userId is { } id ? new NpiAssigneeDto(id, names.GetValueOrDefault(id, UnknownUserName)) : null;

        var clientWorkers = new List<NpiTeamMemberDto>();
        if (clientGrants is not null && npi.LifecycleStatus != Domain.LifecycleStatus.Draft && !string.IsNullOrWhiteSpace(npi.Capture.Cliente))
        {
            foreach (var workerId in await clientGrants.ListWorkersAsync(npi.Capture.Cliente, cancellationToken))
            {
                var worker = await userDirectory.FindAsync(workerId, cancellationToken);
                if (worker is not { Status: Domain.UserStatus.Active }) continue;
                var area = NpiAuthorization.UserAreaOf(new CurrentUser(worker.Id, worker.Role, worker.Area));
                if (area is not { } a || npi.IsResponsible(worker.Id, a) || npi.IsParticipant(worker.Id, a)) continue;
                clientWorkers.Add(new NpiTeamMemberDto(worker.Id, worker.FullName, a.ToString()));
            }
        }

        return new NpiAssigneesDto(
            ToAssignee(npi.AssignedCommercialUserId)!,
            ToAssignee(npi.AssignedEngineeringUserId),
            ToAssignee(npi.AssignedDesignUserId),
            npi.Participants.OrderBy(p => p.AddedAt)
                .Select(p => new NpiTeamMemberDto(p.UserId, names.GetValueOrDefault(p.UserId, UnknownUserName), p.Area.ToString()))
                .ToList(),
            clientWorkers);
    }
}
