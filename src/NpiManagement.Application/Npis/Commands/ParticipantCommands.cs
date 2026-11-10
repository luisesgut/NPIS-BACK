using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Equipo por NPI (2026-11-10): suma a alguien de un área para que trabaje el NPI junto con el
/// responsable. Lo hacen el admin del área, AdminTotal o el propio responsable de esa área.
/// </summary>
public sealed record AddNpiParticipantCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid UserId, AreaType Area);

public sealed class AddNpiParticipantCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications, IUserDirectory userDirectory)
{
    public async Task<NpiDetailDto> HandleAsync(AddNpiParticipantCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAccess(command.Actor, NpiAuthorization.OwnershipOf(npi))) throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanManageParticipants(command.Actor, npi, command.Area))
            throw new RequestValidationException("participants-not-allowed", "Sólo el responsable del área o su admin pueden cambiar el equipo de esa área.");

        await AssigneeGuard.EnsureAssignableAsync(userDirectory, command.UserId, command.Area, cancellationToken);
        npi.AddParticipant(command.UserId, command.Area, new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);
        notifications.Notify(command.UserId, $"{command.ActorName} te agregó al equipo del NPI {npi.NpiNumber?.Value ?? "(borrador)"}: ya puedes trabajarlo.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Saca a alguien del equipo. Además de quien puede agregar, cada quien puede salirse.</summary>
public sealed record RemoveNpiParticipantCommand(Guid NpiId, CurrentUser Actor, string ActorName, Guid UserId, AreaType Area);

public sealed class RemoveNpiParticipantCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RemoveNpiParticipantCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanAccess(command.Actor, NpiAuthorization.OwnershipOf(npi))) throw new NotFoundException("Npi", command.NpiId);
        var self = command.UserId == command.Actor.UserId;
        if (!self && !NpiAuthorization.CanManageParticipants(command.Actor, npi, command.Area))
            throw new RequestValidationException("participants-not-allowed", "Sólo el responsable del área o su admin pueden cambiar el equipo de esa área.");

        npi.RemoveParticipant(command.UserId, command.Area, new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);
        if (!self)
            notifications.Notify(command.UserId, $"{command.ActorName} te quitó del equipo del NPI {npi.NpiNumber?.Value ?? "(borrador)"}.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>
/// A quién se puede sumar al equipo de un área: cuentas activas de esa área que todavía no son
/// responsables ni participantes. Existe porque /api/users es sólo de administradores y el
/// responsable (usuario normal) también invita a compañeros.
/// </summary>
public sealed record GetTeamCandidatesQuery(Guid NpiId, CurrentUser Actor, AreaType Area);

public sealed class GetTeamCandidatesQueryHandler(INpiRepository repository, IUserDirectory userDirectory)
{
    public async Task<IReadOnlyList<Queries.NpiAssigneeDto>> HandleAsync(GetTeamCandidatesQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanAccess(query.Actor, NpiAuthorization.OwnershipOf(npi))) throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanManageParticipants(query.Actor, npi, query.Area)) return [];

        var users = await userDirectory.ListAsync(query.Area, cancellationToken);
        return users
            .Where(u => u.Status == UserStatus.Active && u.Area == query.Area)
            .Where(u => !npi.IsResponsible(u.Id, query.Area) && !npi.IsParticipant(u.Id, query.Area))
            .OrderBy(u => u.FullName)
            .Select(u => new Queries.NpiAssigneeDto(u.Id, u.FullName))
            .ToList();
    }
}
