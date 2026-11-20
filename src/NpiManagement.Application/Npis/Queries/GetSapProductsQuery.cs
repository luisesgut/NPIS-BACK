using System.Text.RegularExpressions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.Npis.Queries;

/// <summary>
/// El PT en SAP del NPI, buscado por el código de su PrintCard final (2026-11-12): Ingeniería ya no lo
/// teclea, sólo lo confirma. Primero exacto; si no hay, sin la revisión ("_R-2"), marcado como aproximado.
/// </summary>
public sealed record GetSapProductsQuery(Guid NpiId, CurrentUser Actor);

public sealed partial class GetSapProductsQueryHandler(INpiRepository repository, ISapProductsReader sap)
{
    [GeneratedRegex(@"^(.+?)_R-?\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex Revision();

    /// <summary>"E-11854-A_R-2" → "E-11854-A"; null si el código no trae revisión.</summary>
    public static string? WithoutRevision(string code) => Revision().Match(code.Trim()) is { Success: true } m ? m.Groups[1].Value : null;

    public async Task<SapProductsLookupDto> HandleAsync(GetSapProductsQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanView(query.Actor, NpiAuthorization.OwnershipOf(npi), npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);

        var code = (npi.FinalPrintCardVersion ?? npi.CurrentPrintCardVersion)?.Code?.Trim();
        if (!sap.IsEnabled) return new SapProductsLookupDto(false, code, []);
        if (string.IsNullOrWhiteSpace(code)) return new SapProductsLookupDto(true, null, []);

        try
        {
            var items = await sap.FindByPrintCardAsync(code, startsWith: false, cancellationToken);
            var approximate = false;
            if (items.Count == 0 && WithoutRevision(code) is { } baseCode)
            {
                items = await sap.FindByPrintCardAsync(baseCode, startsWith: true, cancellationToken);
                approximate = items.Count > 0;
            }
            // Los activos en SAP primero (validFor = 'Y').
            var sorted = items
                .OrderBy(i => string.Equals(i.Estatus, "Y", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.ClaveProducto, StringComparer.Ordinal)
                .ToList();
            return new SapProductsLookupDto(true, code, sorted, approximate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // SAP lento o caído: nunca bloquea; Ingeniería lo captura a mano.
            return new SapProductsLookupDto(true, code, [], Error: "SAP no respondió. Captura el PT a mano o intenta de nuevo.");
        }
    }
}
