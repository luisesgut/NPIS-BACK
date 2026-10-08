namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Textos de aviso con detalle (campana y correo). Antes sólo decían "requiere cambios" y la persona
/// tenía que abrir el NPI y buscar qué se pidió. Se recortan para no exceder el límite del aviso (500).
/// </summary>
public static class NotificationText
{
    private const int MaxDetail = 300;

    public static string ChangesRequested(string? npiNumber, int fieldCount, string description)
    {
        var fields = fieldCount > 0 ? $" ({fieldCount} campo{(fieldCount == 1 ? "" : "s")} por corregir)" : "";
        return $"Ingeniería solicitó cambios en el NPI {npiNumber}{fields}: {Clip(description)}";
    }

    public static string Resubmitted(string? npiNumber, int changedCount, string? comment)
    {
        var changed = changedCount switch
        {
            0 => "sin cambios en los datos capturados",
            1 => "con 1 campo actualizado",
            _ => $"con {changedCount} campos actualizados",
        };
        var text = $"Comercial reenvió el NPI {npiNumber} {changed}.";
        return string.IsNullOrWhiteSpace(comment) ? text : $"{text} Comentario: {Clip(comment)}";
    }

    public static string InformationRequested(string? npiNumber, string text) =>
        $"Diseño solicita información para el NPI {npiNumber}: {Clip(text)}";

    public static string InformationAnswered(string? npiNumber, string response) =>
        $"Comercial respondió a Diseño en el NPI {npiNumber}: {Clip(response)}";

    public static string Comment(string? npiNumber, string author, string text) =>
        $"{author} comentó en el NPI {npiNumber ?? "(borrador)"}: {Clip(text)}";

    private static string Clip(string text)
    {
        var t = text.Trim();
        return t.Length <= MaxDetail ? t : t[..(MaxDetail - 1)] + "…";
    }
}
