using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;

namespace NpiManagement.Application.Npis.Queries;

public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType);

public sealed record GetAttachmentForDownloadQuery(Guid NpiId, Guid AttachmentId, CurrentUser Actor);

public sealed class GetAttachmentForDownloadQueryHandler(INpiRepository repository, IFileStorage fileStorage)
{
    public async Task<AttachmentDownload> HandleAsync(GetAttachmentForDownloadQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        var ownership = new NpiOwnership(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId);
        if (!NpiAuthorization.CanAccess(query.Actor, ownership)) throw new NotFoundException("Npi", query.NpiId);

        var attachment = npi.Attachments.FirstOrDefault(a => a.Id == query.AttachmentId)
            ?? throw new NotFoundException("Attachment", query.AttachmentId);

        if (attachment.IsLink)
            throw new RequestValidationException("attachment-is-link", "Este adjunto es un link: ábrelo directamente desde la lista de adjuntos.");

        var content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new AttachmentDownload(content, attachment.FileName, attachment.ContentType);
    }
}
