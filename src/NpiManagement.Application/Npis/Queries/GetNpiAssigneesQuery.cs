using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.Npis.Queries;

public sealed record NpiAssigneeDto(Guid UserId, string FullName);

/// <summary>Responsables actuales del NPI. Comercial siempre existe; Ingeniería y Diseño pueden no estar asignados todavía.</summary>
public sealed record NpiAssigneesDto(NpiAssigneeDto Commercial, NpiAssigneeDto? Engineering, NpiAssigneeDto? Design);

public sealed record GetNpiAssigneesQuery(Guid NpiId, CurrentUser Actor);

/// <summary>
/// Nombres de los responsables de un NPI, con la misma autorización que su detalle: quien puede
/// ver el NPI puede saber quién lo tiene. Existe aparte de /api/users porque ese listado está
/// reservado a administradores (y cada admin de área sólo ve la suya), así que los demás roles no
/// tenían cómo convertir en nombres los ids del detalle y la pantalla mostraba el id.
/// </summary>
public sealed class GetNpiAssigneesQueryHandler(INpiRepository repository, IUserDirectory userDirectory)
{
    /// <summary>Se muestra si la cuenta ya no existe, en lugar de exponer el id.</summary>
    public const string UnknownUserName = "Usuario no disponible";

    public async Task<NpiAssigneesDto> HandleAsync(GetNpiAssigneesQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken)
            ?? throw new NotFoundException("Npi", query.NpiId);

        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(query.Actor, ownership))
            throw new NotFoundException("Npi", query.NpiId);

        Guid?[] assignedIds = [npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId];
        var names = await userDirectory.GetNamesAsync(
            assignedIds.OfType<Guid>().Distinct().ToList(),
            cancellationToken);

        NpiAssigneeDto? ToAssignee(Guid? userId) =>
            userId is { } id ? new NpiAssigneeDto(id, names.GetValueOrDefault(id, UnknownUserName)) : null;

        return new NpiAssigneesDto(
            ToAssignee(npi.AssignedCommercialUserId)!,
            ToAssignee(npi.AssignedEngineeringUserId),
            ToAssignee(npi.AssignedDesignUserId));
    }
}
