using System.Reflection;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Application.Npis.Queries;

/// <summary>
/// De qué NPI partió éste y qué campos de la captura son distintos (2026-11-11). Sirve para que quien
/// revisa se fije en lo que cambió. Sólo devuelve el folio y los nombres de los campos, no los datos
/// del otro NPI (que quien consulta podría no tener permiso de ver).
/// </summary>
public sealed record NpiBasedOnDto(Guid NpiId, string? NpiNumber, IReadOnlyList<string> ChangedFields);

public sealed record GetNpiBasedOnQuery(Guid NpiId, CurrentUser Actor);

public sealed class GetNpiBasedOnQueryHandler(INpiRepository repository)
{
    // descripcionProducto es copia de descripcion: contarla sería repetir el mismo cambio.
    private static readonly PropertyInfo[] CaptureProperties = typeof(NpiCaptureData).GetProperties()
        .Where(p => p.PropertyType == typeof(string) && p.Name != nameof(NpiCaptureData.DescripcionProducto))
        .ToArray();

    public async Task<NpiBasedOnDto?> HandleAsync(GetNpiBasedOnQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanView(query.Actor, NpiAuthorization.OwnershipOf(npi), npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);
        if (npi.BasedOnNpiId is not { } sourceId) return null;

        var source = await repository.GetByIdAsync(sourceId, cancellationToken);
        if (source is null) return new NpiBasedOnDto(sourceId, null, []);
        return new NpiBasedOnDto(sourceId, source.NpiNumber?.Value, ChangedFields(source.Capture, npi.Capture));
    }

    public static IReadOnlyList<string> ChangedFields(NpiCaptureData before, NpiCaptureData after) =>
        CaptureProperties
            .Where(p => !string.Equals(Normalize(p.GetValue(before)), Normalize(p.GetValue(after)), StringComparison.OrdinalIgnoreCase))
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..])
            .ToList();

    private static string Normalize(object? value) => ((value as string) ?? string.Empty).Trim();
}
