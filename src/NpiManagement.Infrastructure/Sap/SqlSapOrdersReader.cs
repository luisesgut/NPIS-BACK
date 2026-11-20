using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.Sap;

/// <summary>
/// Lee la vista dbo.Cat_Ordenes filtrando por ClaveProducto (el PT). Consulta parametrizada y de sólo
/// lectura; nunca se trae la vista completa.
/// </summary>
public sealed class SqlSapOrdersReader(IOptions<SapOrdersOptions> options) : ISapOrdersReader
{
    private const string Sql = @"
SELECT Orden, ClaveProducto, Producto, Cantidad, Unidad, NombreCliente, Vendedor, Sucursal
FROM dbo.Cat_Ordenes
WHERE ClaveProducto = @clave
ORDER BY Orden";

    public bool IsEnabled => options.Value.IsConfigured;

    public async Task<IReadOnlyList<SapOrderDto>> FindByProductKeyAsync(string claveProducto, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) return [];

        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(Sql, connection) { CommandTimeout = Math.Clamp(settings.TimeoutSeconds, 3, 60) };
        command.Parameters.Add(new SqlParameter("@clave", System.Data.SqlDbType.NVarChar, 50) { Value = claveProducto.Trim() });

        var result = new List<SapOrderDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SapOrderDto(
                Text(reader, 0) ?? string.Empty,
                Text(reader, 1) ?? string.Empty,
                Text(reader, 2),
                reader.IsDBNull(3) ? null : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                Text(reader, 4),
                Text(reader, 5),
                Text(reader, 6),
                Text(reader, 7)));
        }
        return result;
    }

    private static string? Text(SqlDataReader reader, int i) =>
        reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim();
}
