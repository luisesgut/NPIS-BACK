namespace NpiManagement.Domain.Entities;

/// <summary>
/// Imágenes (capturas de pantalla, fotos) adjuntas a una solicitud de cambios o de información
/// (2026-11-04): son Attachments normales del NPI; la solicitud sólo guarda sus ids como texto
/// separado por comas (como FieldKeys), sin tabla propia.
/// </summary>
internal static class RequestImages
{
    public const int Max = 10;

    public static string? Join(IReadOnlyList<Guid> ids) => ids.Count == 0 ? null : string.Join(',', ids);

    public static IReadOnlyList<Guid> Parse(string? csv) =>
        string.IsNullOrEmpty(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();
}
