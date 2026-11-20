namespace NpiManagement.Application.Users;

/// <summary>Una persona del equipo comercial (id y nombre).</summary>
public sealed record TeamPersonDto(Guid Id, string FullName);

/// <summary>
/// "Mi equipo" de quien consulta (2026-11-18): su director (si es vendedor con director) y sus
/// vendedores (si es director). Un vendedor no ve a sus compañeros.
/// </summary>
public sealed record MyTeamDto(TeamPersonDto? Director, IReadOnlyList<TeamPersonDto> Members);

/// <summary>Relación director de Comercial ↔ vendedores (columna DirectorUserId del usuario).</summary>
public interface ICommercialTeamStore
{
    /// <summary>El director del vendedor, con caché corta: se consulta en cada petición (claims).</summary>
    Task<Guid?> GetDirectorIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Vendedores activos cuyo director es <paramref name="directorUserId"/>.</summary>
    Task<IReadOnlyList<TeamPersonDto>> ListMembersAsync(Guid directorUserId, CancellationToken cancellationToken);

    Task<MyTeamDto> GetMyTeamAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Directores de todos los usuarios que tienen uno (para la pantalla de Usuarios).</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ListDirectorsAsync(CancellationToken cancellationToken);

    Task SetDirectorAsync(Guid userId, Guid? directorUserId, CancellationToken cancellationToken);
}
