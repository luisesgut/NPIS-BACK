using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.Sap;

/// <summary>
/// Lee dbo.Cat_Productos (misma base y conexión que las órdenes) filtrando por el campo PrintCard del
/// artículo. Consulta parametrizada y de sólo lectura.
/// </summary>
public sealed class SqlSapProductsReader(IOptions<SapOrdersOptions> options) : ISapProductsReader
{
    private const string Select = "SELECT ClaveProducto, NombreProducto, PrintCard, Estatus FROM dbo.Cat_Productos ";
    private const string Exact = "WHERE PrintCard = @pc ORDER BY ClaveProducto";
    // En LIKE los comodines del código (_ y %) se escapan con '!' para que cuenten como texto.
    private const string Prefix = "WHERE PrintCard LIKE @pc ESCAPE '!' ORDER BY ClaveProducto";

    public bool IsEnabled => options.Value.IsConfigured;

    public async Task<IReadOnlyList<SapProductDto>> FindByPrintCardAsync(string printCardCode, bool startsWith, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) return [];

        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(Select + (startsWith ? Prefix : Exact), connection)
        {
            CommandTimeout = Math.Clamp(settings.TimeoutSeconds, 3, 60),
        };
        var value = printCardCode.Trim();
        if (startsWith)
            value = value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_").Replace("[", "![") + "%";
        command.Parameters.Add(new SqlParameter("@pc", System.Data.SqlDbType.NVarChar, 120) { Value = value });

        var result = new List<SapProductDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new SapProductDto(Text(reader, 0) ?? string.Empty, Text(reader, 1), Text(reader, 2), Text(reader, 3)));
        return result;
    }

    private static string? Text(SqlDataReader reader, int i) =>
        reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim();
}
