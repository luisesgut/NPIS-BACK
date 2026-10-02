namespace NpiManagement.Application.Attachments;

/// <summary>
/// Valida la firma binaria (magic bytes) real del archivo contra el Content-Type declarado —
/// nunca se confía sólo en la extensión o el Content-Type que envía el cliente (instrucción
/// explícita del pedido). Lógica pura, sin dependencias de E/S.
/// </summary>
public static class FileSignatureValidator
{
    public static bool MatchesContentType(ReadOnlySpan<byte> header, string contentType) => contentType switch
    {
        "application/pdf" => StartsWith(header, [0x25, 0x50, 0x44, 0x46]), // %PDF
        "image/jpeg" => StartsWith(header, [0xFF, 0xD8, 0xFF]),
        "image/png" => StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        _ => false,
    };

    private static bool StartsWith(ReadOnlySpan<byte> header, ReadOnlySpan<byte> signature) =>
        header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);
}
