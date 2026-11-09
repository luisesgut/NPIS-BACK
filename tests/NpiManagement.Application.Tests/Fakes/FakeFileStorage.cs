using System.Security.Cryptography;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Application.Tests.Fakes;

public sealed class FakeFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _store = [];

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        _store.Remove(storageKey);
        return Task.CompletedTask;
    }

    public Task<StoredFile> SaveAsync(Stream content, string sanitizedFileName, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var key = Guid.NewGuid().ToString("N");
        _store[key] = bytes;
        return Task.FromResult(new StoredFile(key, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes))));
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream(_store[storageKey]));
}

public sealed class FakeAttachmentPolicy : IAttachmentPolicy
{
    public long MaxSizeBytes { get; init; } = 10 * 1024 * 1024;
    public HashSet<string> AllowedContentTypes { get; init; } = ["application/pdf", "image/jpeg", "image/png"];

    public bool IsAllowedContentType(string contentType) => AllowedContentTypes.Contains(contentType);
}
