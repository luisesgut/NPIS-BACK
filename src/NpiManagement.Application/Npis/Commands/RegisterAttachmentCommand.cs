using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Attachments;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Sube un archivo y lo registra como metadato del NPI (docs/api-contract.md §7). El contenido
/// nunca se persiste en la fila del NPI (ADR-021) — sólo lo referencia IFileStorage. Autorización:
/// cualquiera con acceso de lectura al NPI puede adjuntar (igual alcance que agregar comentarios).
/// </summary>
public sealed record RegisterAttachmentCommand(Guid NpiId, CurrentUser Actor, string ActorName, string FileName, string ContentType, long DeclaredSizeBytes, Stream Content, AttachmentContext Context);

public sealed class RegisterAttachmentCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IFileStorage fileStorage, IAttachmentPolicy policy)
{
    public async Task<AttachmentDto> HandleAsync(RegisterAttachmentCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(command.Actor, ownership)) throw new NotFoundException("Npi", command.NpiId);

        if (!policy.IsAllowedContentType(command.ContentType))
            throw new RequestValidationException("attachment-content-type-not-allowed", $"El tipo de archivo '{command.ContentType}' no está permitido. Formatos soportados: PDF, JPG o PNG.");
        if (command.DeclaredSizeBytes <= 0 || command.DeclaredSizeBytes > policy.MaxSizeBytes)
            throw new RequestValidationException("attachment-too-large", $"El archivo excede el tamaño máximo permitido ({FormatMaxSize(policy.MaxSizeBytes)}).");

        var sanitizedFileName = FileNameSanitizer.Sanitize(command.FileName);

        // Lee el encabezado para validar la firma real del archivo (nunca confiar sólo en el
        // Content-Type declarado por el cliente) sin consumir el stream que usará IFileStorage.
        using var buffered = new MemoryStream();
        await command.Content.CopyToAsync(buffered, cancellationToken);
        if (buffered.Length > policy.MaxSizeBytes)
            throw new RequestValidationException("attachment-too-large", $"El archivo excede el tamaño máximo permitido ({FormatMaxSize(policy.MaxSizeBytes)}).");

        buffered.Position = 0;
        var header = new byte[Math.Min(8, buffered.Length)];
        _ = await buffered.ReadAsync(header, cancellationToken);
        if (!FileSignatureValidator.MatchesContentType(header, command.ContentType))
            throw new RequestValidationException("attachment-signature-mismatch", "El contenido del archivo no corresponde a un PDF, JPG o PNG válido (la extensión no coincide con el contenido real del archivo).");

        buffered.Position = 0;
        var stored = await fileStorage.SaveAsync(buffered, sanitizedFileName, cancellationToken);

        var actor = new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName);
        var attachment = npi.RegisterAttachment(actor, clock, sanitizedFileName, command.ContentType, stored.SizeBytes, stored.StorageKey, stored.Sha256Hash, command.Context);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.Context.ToString(), attachment.Version, attachment.UploadedByUserId, attachment.UploadedAt);
    }

    private static string FormatMaxSize(long maxSizeBytes) => $"{maxSizeBytes / (1024 * 1024)} MB";
}
