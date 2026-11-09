using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis;

/// <summary>
/// El precio de la cotización es de Comercial: Ingeniería y Diseño ven el estado de la cotización,
/// las cantidades y las diferencias de estructura, pero no los montos. Se aplica en un solo lugar
/// (filtro de la API) para no tener que acordarse en cada endpoint que devuelve un NPI.
/// </summary>
public static class QuotePriceRedaction
{
    public static bool CanSeePrices(string role) =>
        role is ApplicationRoles.AdminTotal or ApplicationRoles.AdminComercial or ApplicationRoles.UsuarioComercial;

    public static NpiDetailDto Redact(NpiDetailDto dto) =>
        dto with { SisproQuotes = dto.SisproQuotes.Select(q => q with { Snapshot = Redact(q.Snapshot) }).ToList() };

    public static QuotationSnapshot? Redact(QuotationSnapshot? snapshot) =>
        snapshot is null ? null : snapshot with
        {
            Cantidades = snapshot.Cantidades.Select(c => c with { PrecioUnidadVenta = null, PrecioTotal = null }).ToList(),
        };
}
