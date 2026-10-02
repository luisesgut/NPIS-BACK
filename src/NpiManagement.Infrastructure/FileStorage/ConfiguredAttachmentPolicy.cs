using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.FileStorage;

public sealed class ConfiguredAttachmentPolicy(IOptions<FileStorageOptions> options) : IAttachmentPolicy
{
    public long MaxSizeBytes => options.Value.MaxSizeBytes;

    public bool IsAllowedContentType(string contentType) =>
        options.Value.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
}
