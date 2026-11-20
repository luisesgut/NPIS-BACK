using System.Text.RegularExpressions;

namespace NpiManagement.Domain.ValueObjects;

/// <summary>
/// Número de folio del NPI, formato "NPI-####" (4 dígitos mínimo, sin tope superior).
/// La generación atómica (SEQUENCE de SQL Server) es responsabilidad de Infrastructure
/// (ver docs/architecture-decisions.md ADR-005); este VO sólo valida forma e igualdad.
/// </summary>
public sealed partial class NpiNumber : IEquatable<NpiNumber>
{
    public string Value { get; }

    private NpiNumber(string value) => Value = value;

    /// <summary>Construye el número a partir del valor entero de la secuencia (ej. 4 -&gt; "NPI-0004").</summary>
    public static NpiNumber FromSequenceValue(long sequenceValue)
    {
        if (sequenceValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequenceValue), "El valor de secuencia debe ser positivo.");

        return new NpiNumber($"NPI-{sequenceValue:0000}");
    }

    /// <summary>Reconstruye el VO desde un valor ya persistido (o un número semilla, ej. "NPI-1006").</summary>
    public static NpiNumber FromExisting(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !FormatRegex().IsMatch(value))
            throw new ArgumentException($"'{value}' no tiene el formato esperado 'NPI-####'.", nameof(value));

        return new NpiNumber(value);
    }

    /// <summary>
    /// Folio que alguien escribió en un buscador (2026-11-20): "NPI-0080", "npi 80", "0080" u "80" son NPI-0080.
    /// Sólo si el texto es un folio; cualquier otra cosa (un cliente, una cotización con letras) no.
    /// </summary>
    public static bool TryParseSearch(string? text, out NpiNumber? number)
    {
        number = null;
        var match = SearchRegex().Match(text?.Trim() ?? string.Empty);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, out var value) || value <= 0) return false;
        number = FromSequenceValue(value);
        return true;
    }

    [GeneratedRegex(@"^NPI-\d{4,}$")]
    private static partial Regex FormatRegex();

    [GeneratedRegex(@"^(?:NPI[\s-]*)?(\d{1,9})$", RegexOptions.IgnoreCase)]
    private static partial Regex SearchRegex();

    public bool Equals(NpiNumber? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as NpiNumber);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
