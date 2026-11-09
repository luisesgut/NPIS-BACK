using System.Text.RegularExpressions;
using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.ClientRecipients;

/// <summary>
/// Libreta de correos de cada vendedor por cliente (2026-11-09), para "Enviar al cliente": el sistema
/// no manda el correo, arma un borrador de Outlook (escritorio) con estos destinatarios y el PrintCard
/// adjunto. Kind = "To" (Para) o "Cc"; Selected = lo que palomeó la última vez (se vuelve a proponer).
/// </summary>
public sealed record ClientRecipientDto(string Email, string Kind, bool Selected);

public interface IClientRecipientStore
{
    Task<IReadOnlyList<ClientRecipientDto>> ListAsync(Guid userId, string cliente, CancellationToken cancellationToken);

    /// <summary>Reemplaza la libreta de ese vendedor para ese cliente (lo que no venga, se borra).</summary>
    Task ReplaceAsync(Guid userId, string cliente, IReadOnlyList<ClientRecipientDto> recipients, CancellationToken cancellationToken);
}

public sealed partial class ClientRecipientsService(IClientRecipientStore store)
{
    public const int MaxRecipients = 30;
    public const int EmailMaxLength = 254;

    public Task<IReadOnlyList<ClientRecipientDto>> ListAsync(Guid userId, string? cliente, CancellationToken cancellationToken) =>
        store.ListAsync(userId, NormalizeCliente(cliente), cancellationToken);

    public async Task<IReadOnlyList<ClientRecipientDto>> SaveAsync(Guid userId, string? cliente, IReadOnlyList<ClientRecipientDto>? recipients, CancellationToken cancellationToken)
    {
        var key = NormalizeCliente(cliente);
        var clean = Clean(recipients ?? []);
        await store.ReplaceAsync(userId, key, clean, cancellationToken);
        return clean;
    }

    /// <summary>Correos en minúsculas y sin repetir; valida formato, tipo y cantidad.</summary>
    public static IReadOnlyList<ClientRecipientDto> Clean(IReadOnlyList<ClientRecipientDto> recipients)
    {
        var result = new List<ClientRecipientDto>();
        foreach (var r in recipients)
        {
            var email = (r.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (email.Length == 0) continue;
            if (email.Length > EmailMaxLength || !EmailPattern().IsMatch(email))
                throw new RequestValidationException("recipient-email-invalid", $"\"{r.Email}\" no parece un correo válido.");
            var kind = string.Equals(r.Kind, "Cc", StringComparison.OrdinalIgnoreCase) ? "Cc" : "To";
            if (result.Any(x => x.Email == email)) continue;
            result.Add(new ClientRecipientDto(email, kind, r.Selected));
        }
        if (result.Count > MaxRecipients)
            throw new RequestValidationException("recipients-too-many", $"Máximo {MaxRecipients} correos por cliente.");
        return result;
    }

    // Los nombres de cliente se guardan en mayúsculas (2026-11-09): la libreta usa la misma llave.
    private static string NormalizeCliente(string? cliente)
    {
        var key = (cliente ?? string.Empty).Trim().ToUpperInvariant();
        if (key.Length == 0) throw new RequestValidationException("recipients-cliente-required", "Falta el cliente.");
        if (key.Length > 200) throw new RequestValidationException("recipients-cliente-too-long", "El nombre del cliente es demasiado largo.");
        return key;
    }

    [GeneratedRegex(@"^[^@\s;,<>""]+@[^@\s;,<>""]+\.[^@\s;,<>""]{2,}$")]
    private static partial Regex EmailPattern();
}
