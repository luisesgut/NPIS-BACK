using NpiManagement.Application.PrintCards;
using NpiManagement.Domain.Common;

namespace NpiManagement.Infrastructure.Export;

/// <summary>Excel del historial de PrintCards (2026-11-23): una fila por PrintCard, con los filtros que tenga la pantalla.</summary>
public sealed class PrintCardReportExporter(IClock clock) : IPrintCardReportExporter
{
    private static readonly TimeSpan MexicoOffset = TimeSpan.FromHours(-6);

    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["en_proceso"] = "En proceso", ["esperando_cliente"] = "Esperando al cliente", ["cambios_cliente"] = "Cliente pidió cambios",
        ["aprobado"] = "Aprobado por el cliente", ["cancelado"] = "NPI cancelado o rechazado",
    };

    public PrintCardExportFile Build(IReadOnlyList<PrintCardRecordDto> records)
    {
        string[] headers =
        [
            "Código PC", "NPI", "Cliente", "Producto", "Diseñador", "Versiones", "Rechazos de Ingeniería", "Cambios del cliente",
            "Primera versión", "Enviado al cliente", "Tiempo activo (h hábiles)", "Días naturales", "Salió a la primera", "Estado",
        ];
        var rows = records.Select(r => (IReadOnlyList<string?>)
        [
            r.CurrentCode, r.NpiNumber, r.Cliente, r.DescripcionProducto, r.DesignerName, r.Versions.ToString(),
            r.EngineeringRejections.ToString(), r.ClientChangeRounds.ToString(), Local(r.FirstUploadedAt),
            r.FirstSentAt is { } sent ? Local(sent) : null, r.ActiveHours?.ToString("0.0"), r.CalendarDays?.ToString("0.0"),
            r.FirstPass ? "Sí" : "No", StatusLabels.GetValueOrDefault(r.Status, r.Status),
        ]);
        var content = XlsxWriter.Build("PrintCards", headers, rows, [16, 11, 24, 36, 24, 10, 12, 12, 17, 17, 14, 12, 12, 24]);
        return new PrintCardExportFile(content, XlsxWriter.ContentType, $"printcards-{clock.UtcNow.ToOffset(MexicoOffset):yyyy-MM-dd}.xlsx");
    }

    private static string Local(DateTimeOffset at) => at.ToOffset(MexicoOffset).ToString("dd/MM/yyyy HH:mm");
}
