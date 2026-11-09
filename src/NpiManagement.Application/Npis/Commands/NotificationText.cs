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

    public static string DesignChangesRequested(string? npiNumber, int fieldCount, string description)
    {
        var fields = fieldCount > 0 ? $" ({fieldCount} campo{(fieldCount == 1 ? "" : "s")} por corregir)" : "";
        return $"Diseño solicitó cambios en el NPI {npiNumber}{fields}: {Clip(description)}";
    }

    public static string ClosureProposed(string? npiNumber, string proposedBy, string closure, string note) =>
        $"{proposedBy} propone cerrar el NPI {npiNumber} por {closure} y espera tu visto bueno: {Clip(note)}";

    public static string ClosureDeclined(string? npiNumber, string adminName, string comment) =>
        $"{adminName} no autorizó cerrar el NPI {npiNumber}; el NPI sigue en curso: {Clip(comment)}";

    public static string EngineeringNoFeasibility(string? npiNumber, string? note) =>
        string.IsNullOrWhiteSpace(note)
            ? $"El NPI {npiNumber} fue rechazado por no factibilidad."
            : $"El NPI {npiNumber} fue rechazado por Ingeniería (no factible): {Clip(note)}";

    public static string DesignNoFeasibility(string? npiNumber, string note) =>
        $"El NPI {npiNumber} fue rechazado por Diseño (no factible): {Clip(note)}";

    public static string ArtworkRejected(string? npiNumber, string reason) =>
        $"Diseño rechazó las artes del NPI {npiNumber}: {Clip(reason)}";

    public static string PrintCardRejected(string? npiNumber, int? version, string reason) =>
        $"Ingeniería rechazó el PrintCard v{version} del NPI {npiNumber}: {Clip(reason)}";

    public static string ArtworkResubmitted(string? npiNumber, string? comment)
    {
        var text = $"Comercial subió artes corregidas en el NPI {npiNumber}.";
        return string.IsNullOrWhiteSpace(comment) ? text : $"{text} Comentario: {Clip(comment)}";
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

    public static string ReconfirmationRequested(string? npiNumber, int fieldCount, bool hasPt, bool hasEt)
    {
        var text = $"Comercial cambió {fieldCount} campo{(fieldCount == 1 ? "" : "s")} que ya habías revisado en el NPI {npiNumber}. Reconfirma la factibilidad (el PrintCard no puede ir al cliente hasta entonces).";
        var reminder = (hasPt, hasEt) switch
        {
            (true, true) => " Revisa si hay que actualizar el PT y la ET.",
            (true, false) => " Revisa si hay que actualizar el PT.",
            (false, true) => " Revisa si hay que actualizar la ET.",
            _ => "",
        };
        return text + reminder;
    }

    public static string EngineeringFeasibilityApproved(string? npiNumber, string? note)
    {
        var text = $"Ingeniería aprobó la factibilidad del NPI {npiNumber}.";
        return string.IsNullOrWhiteSpace(note) ? text : $"{text} Nota: {Clip(note)}";
    }

    public static string Reconfirmed(string? npiNumber, string? note)
    {
        var text = $"Ingeniería reconfirmó la factibilidad del NPI {npiNumber} con los cambios de Comercial.";
        return string.IsNullOrWhiteSpace(note) ? text : $"{text} Nota: {Clip(note)}";
    }

    public static string InformationRequested(string? npiNumber, string text) =>
        $"Diseño necesita que consigas con el cliente para el NPI {npiNumber}: {Clip(text)}";

    public static string EngineeringQuestion(string? npiNumber, string text) =>
        $"Diseño tiene dudas que sólo Ingeniería puede aclarar en el NPI {npiNumber}: {Clip(text)}";

    public static string EngineeringAnswered(string? npiNumber, string response) =>
        $"Ingeniería contestó tus dudas del NPI {npiNumber}: {Clip(response)}";

    public static string RequestEdited(string? npiNumber, string text) =>
        $"Se actualizó la solicitud que tienes pendiente en el NPI {npiNumber}: {Clip(text)}";

    public static string RequestWithdrawn(string? npiNumber, string area) =>
        $"{area} retiró la solicitud que tenías pendiente en el NPI {npiNumber}: ya no tienes que atenderla.";

    public static string InformationAnswered(string? npiNumber, string response) =>
        $"Comercial te entregó lo que pediste al cliente en el NPI {npiNumber}: {Clip(response)}";

    /// <summary>Aviso agrupado del chat: se reescribe con el último mensaje mientras no se lea.</summary>
    public static string ChatMessage(string? npiNumber, string author, string text) =>
        $"Nuevos mensajes en el chat del NPI {npiNumber ?? "(borrador)"} — {author}: {Clip(text)}";

    public static string Mention(string? npiNumber, string author, string text) =>
        $"{author} te mencionó en el chat del NPI {npiNumber ?? "(borrador)"}: {Clip(text)}";

    /// <summary>Clave con la que se agrupan los avisos del chat de un NPI (uno por NPI y destinatario).</summary>
    public static string ChatGroupKey(Guid npiId) => $"chat:{npiId:N}";

    private static string Clip(string text)
    {
        var t = text.Trim();
        return t.Length <= MaxDetail ? t : t[..(MaxDetail - 1)] + "…";
    }
}
