using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.Advance;

/// <summary>Cliente de sólo lectura de Advance (estado de documentos y PDF del PrintCard).</summary>
public sealed class AdvanceClient(HttpClient http, IOptions<AdvanceOptions> options) : IAdvanceClient
{
    public bool IsEnabled => options.Value.Habilitado;

    public DateTimeOffset? AppliesFrom => options.Value.AplicarDesde;

    public async Task<AdvanceStatusDto?> GetStatusAsync(string printCardCode, CancellationToken cancellationToken)
    {
        var url = $"{options.Value.EstadoUrl.TrimEnd('/', '?')}?codigo={Uri.EscapeDataString(printCardCode.Trim())}";
        using var response = await http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<EstadoResponse>(cancellationToken);
        if (body is null) return null;
        var docs = (body.Documentos ?? []).Select(d => new AdvanceDocumentDto(d.Tipo ?? string.Empty, d.Nombre ?? d.Tipo ?? string.Empty, d.Subido)).ToList();
        return new AdvanceStatusDto(
            body.Codigo ?? printCardCode, body.ExisteEnCatalogo, body.Descripcion, body.CodigoSap,
            body.Subidos, body.TotalDocumentos, body.Completo, docs, ArchivoUrl(printCardCode));
    }

    public async Task<AdvanceFile?> DownloadPrintCardAsync(string printCardCode, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(ArchivoUrl(printCardCode), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
        // Sólo PDF: es lo que guarda Advance y lo que el detalle sabe mostrar como PrintCard.
        if (!contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)) return null;
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new AdvanceFile(content, fileName?.Trim('"') ?? $"{printCardCode.Trim()}.pdf", contentType);
    }

    private string ArchivoUrl(string code) => $"{options.Value.ArchivoUrl.TrimEnd('/')}/{Uri.EscapeDataString(code.Trim())}";

    private sealed class EstadoResponse
    {
        public string? Codigo { get; set; }
        public bool ExisteEnCatalogo { get; set; }
        public string? Descripcion { get; set; }
        [JsonPropertyName("codigoSAP")] public string? CodigoSap { get; set; }
        public bool Completo { get; set; }
        public int TotalDocumentos { get; set; }
        public int Subidos { get; set; }
        public List<Documento>? Documentos { get; set; }
    }

    private sealed class Documento
    {
        public string? Tipo { get; set; }
        public string? Nombre { get; set; }
        public bool Subido { get; set; }
    }
}
