using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.Clients;

/// <summary>Un nombre de cliente tal como está escrito en los NPIs y cuántos lo usan.</summary>
public sealed record ClientNameDto(string Name, int NpiCount);

/// <summary>
/// Nombres de cliente capturados en los NPIs (texto libre: cada asesor lo escribía a su manera —
/// "Mr. Lucky" y "Mr Lucky" salían como dos clientes). Implementado en Infrastructure con SQL directo.
/// </summary>
public interface IClientNameStore
{
    Task<IReadOnlyList<ClientNameDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Cambia el cliente de todos los NPIs que tengan alguno de <paramref name="from"/> por <paramref name="to"/>; devuelve cuántos NPIs cambió.</summary>
    Task<int> RenameAsync(IReadOnlyCollection<string> from, string to, CancellationToken cancellationToken);
}

public sealed class ListClientNamesQueryHandler(IClientNameStore store)
{
    public Task<IReadOnlyList<ClientNameDto>> HandleAsync(CancellationToken cancellationToken) => store.ListAsync(cancellationToken);
}

public sealed record MergeClientNamesCommand(IReadOnlyList<string> From, string To);

/// <summary>
/// Unificar nombres de cliente (2026-11-04, AdminTotal e Ingeniería). Es limpieza de un dato de
/// catálogo, no un evento del NPI: a propósito NO deja entrada en la bitácora de cada NPI, no
/// reinicia relojes ni toca el flujo — sólo corrige cómo está escrito el cliente.
/// </summary>
public sealed class MergeClientNamesCommandHandler(IClientNameStore store)
{
    /// <summary>Mismo límite que la columna Capture_Cliente.</summary>
    public const int MaxLength = 200;
    private const int MaxNamesPerMerge = 50;

    public Task<int> HandleAsync(MergeClientNamesCommand command, CancellationToken cancellationToken)
    {
        // Los clientes se guardan en MAYÚSCULAS (2026-11-09): el nombre unificado también.
        var to = (command.To ?? string.Empty).Trim().ToUpperInvariant();
        if (to.Length == 0)
            throw new RequestValidationException("ClientNameRequired", "Escribe el nombre correcto del cliente.");
        if (to.Length > MaxLength)
            throw new RequestValidationException("ClientNameTooLong", $"El nombre del cliente no puede pasar de {MaxLength} caracteres.");

        // Se comparan tal cual están guardados (con sus espacios), para encontrar exactamente esos NPIs.
        var from = (command.From ?? [])
            .Where(n => !string.IsNullOrEmpty(n) && n != to)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (from.Count == 0)
            throw new RequestValidationException("ClientNamesRequired", "Elige al menos un nombre distinto al correcto para unificarlo.");
        if (from.Count > MaxNamesPerMerge)
            throw new RequestValidationException("TooManyClientNames", $"Unifica máximo {MaxNamesPerMerge} nombres a la vez.");

        return store.RenameAsync(from, to, cancellationToken);
    }
}
