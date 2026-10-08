using System.Text.Json;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Domain.ValueObjects;

/// <summary>
/// Comparación campo por campo de dos capturas y foto en JSON de una captura. Lo usan las
/// solicitudes de cambio (qué corrigió Comercial al reenviar) y la bitácora de cambios del NPI.
/// Las claves van en camelCase, igual que en el frontend.
/// </summary>
public static class CaptureDiff
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Snapshot(NpiCaptureData capture) => JsonSerializer.Serialize(capture, Json);

    public static NpiCaptureData? FromSnapshot(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<NpiCaptureData>(json, Json);

    public static IReadOnlyList<FieldChange> Between(NpiCaptureData before, NpiCaptureData after)
    {
        var changes = new List<FieldChange>();
        foreach (var property in typeof(NpiCaptureData).GetProperties())
        {
            if (property.PropertyType != typeof(string)) continue;
            var oldValue = (string?)property.GetValue(before) ?? string.Empty;
            var newValue = (string?)property.GetValue(after) ?? string.Empty;
            if (oldValue != newValue)
                changes.Add(new FieldChange(char.ToLowerInvariant(property.Name[0]) + property.Name[1..], oldValue, newValue));
        }
        return changes;
    }
}
