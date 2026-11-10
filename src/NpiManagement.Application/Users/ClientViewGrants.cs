using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.Users;

/// <summary>
/// "Clientes que puede ver" (2026-11-10): AdminTotal le da a un usuario normal permiso de CONSULTA
/// sobre todos los NPIs de ciertos clientes, aunque no los haya creado ni esté asignado. Sólo ver:
/// operar, escribir en el chat o subir archivos sigue exigiendo ser dueño o responsable
/// (NpiAuthorization.CanAccess); la consulta usa NpiAuthorization.CanView.
/// </summary>
public interface IClientViewGrantStore
{
    /// <summary>Clientes (normalizados) que el usuario puede consultar y, de ésos, los que puede trabajar. Se cachea poco tiempo: se pide en cada petición.</summary>
    Task<ClientGrants> GetGrantsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClientViewGrantDto>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task ReplaceAsync(Guid userId, IReadOnlyList<ClientViewGrantDto> grants, Guid changedByUserId, CancellationToken cancellationToken);

    /// <summary>Quiénes tienen "Ver y trabajar" sobre ese cliente (para mostrarlos en el equipo del NPI).</summary>
    Task<IReadOnlyList<Guid>> ListWorkersAsync(string cliente, CancellationToken cancellationToken);
}

public sealed record ClientGrants(IReadOnlySet<string> View, IReadOnlySet<string> Work);

/// <summary>Un cliente compartido: sólo ver, o ver y trabajar (2026-11-10).</summary>
public sealed record ClientViewGrantDto(string Cliente, bool CanWork);

public static class ClientViewGrants
{
    public const int MaxClients = 50;
    public const int ClienteMaxLength = 200;

    /// <summary>Los nombres de cliente se guardan en MAYÚSCULAS (2026-11-09): se compara igual.</summary>
    public static string Normalize(string cliente) => string.Join(' ', cliente.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static IReadOnlyList<string> Clean(IEnumerable<string>? clientes)
    {
        var list = (clientes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(Normalize)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        if (list.Count > MaxClients)
            throw new RequestValidationException("client-views-too-many", $"Máximo {MaxClients} clientes por usuario.");
        if (list.Any(c => c.Length > ClienteMaxLength))
            throw new RequestValidationException("client-views-cliente-too-long", $"El nombre del cliente no puede pasar de {ClienteMaxLength} caracteres.");
        return list;
    }

    /// <summary>Lo mismo que <see cref="Clean"/>; "trabajar" sólo cuenta para clientes que también están en la lista.</summary>
    public static IReadOnlyList<ClientViewGrantDto> CleanGrants(IEnumerable<string>? clientes, IEnumerable<string>? trabajar)
    {
        var work = (trabajar ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(Normalize).ToHashSet(StringComparer.Ordinal);
        return Clean(clientes).Select(c => new ClientViewGrantDto(c, work.Contains(c))).ToList();
    }
}
