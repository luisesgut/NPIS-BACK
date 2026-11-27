using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.Npis.Queries;

/// <summary>
/// Órdenes de venta de SAP con el PT del NPI (2026-11-12), para que Comercial no la busque a mano: la
/// primera (la más antigua por FechaCreacion; a igual fecha, el número más bajo) es la que corresponde. Si SAP no responde, se avisa y se captura a mano.
/// </summary>
public sealed record GetSapOrdersQuery(Guid NpiId, CurrentUser Actor);

public sealed class GetSapOrdersQueryHandler(INpiRepository repository, ISapOrdersReader sap)
{
    public async Task<SapOrdersLookupDto> HandleAsync(GetSapOrdersQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanView(query.Actor, NpiAuthorization.OwnershipOf(npi), npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);

        var clave = npi.CodigoPT?.Trim();
        if (!sap.IsEnabled) return new SapOrdersLookupDto(false, clave, []);
        if (string.IsNullOrWhiteSpace(clave)) return new SapOrdersLookupDto(true, null, []);

        try
        {
            var orders = await sap.FindByProductKeyAsync(clave, cancellationToken);
            // La más antigua primero (2026-11-27); sin fecha, al final. Orden es numérica en SAP: se ordena como
            // número, no como texto ("9999" antes que "10000").
            var sorted = orders
                .OrderBy(o => o.FechaCreacion ?? DateOnly.MaxValue)
                .ThenBy(o => long.TryParse(o.Orden, out var n) ? n : long.MaxValue)
                .ThenBy(o => o.Orden, StringComparer.Ordinal)
                .ToList();
            return new SapOrdersLookupDto(true, clave, sorted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // SAP lento o caído: nunca bloquea; Comercial lo captura a mano.
            return new SapOrdersLookupDto(true, clave, [], "SAP no respondió. Captura el número de pedido a mano o intenta de nuevo.");
        }
    }
}
