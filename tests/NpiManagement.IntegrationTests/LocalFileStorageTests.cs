using FluentAssertions;
using Microsoft.Extensions.Options;
using NpiManagement.Infrastructure.FileStorage;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// E/S real contra disco local, en un directorio temporal propio de la prueba — no requiere
/// Docker ni SQL Server, corre siempre (a diferencia del resto de esta carpeta).
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "NpiManagement.Tests", Guid.NewGuid().ToString("N"));
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_writes_the_file_and_returns_a_matching_size_and_hash()
    {
        var bytes = "%PDF-1.4 contenido de prueba"u8.ToArray();

        var stored = await _storage.SaveAsync(new MemoryStream(bytes), "documento.pdf", CancellationToken.None);

        stored.SizeBytes.Should().Be(bytes.Length);
        stored.Sha256Hash.Should().Be(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        stored.StorageKey.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task SaveAsync_never_uses_the_original_file_name_as_the_storage_key()
    {
        var stored = await _storage.SaveAsync(new MemoryStream([1, 2, 3]), "../../etc/passwd.pdf", CancellationToken.None);

        stored.StorageKey.Should().NotContain("passwd");
        stored.StorageKey.Should().NotContain("..");
    }

    [Fact]
    public async Task OpenReadAsync_returns_the_exact_bytes_that_were_saved()
    {
        var original = "contenido de la prueba de lectura"u8.ToArray();
        var stored = await _storage.SaveAsync(new MemoryStream(original), "nota.pdf", CancellationToken.None);

        await using var readBack = await _storage.OpenReadAsync(stored.StorageKey, CancellationToken.None);
        using var buffer = new MemoryStream();
        await readBack.CopyToAsync(buffer);

        buffer.ToArray().Should().Equal(original);
    }

    [Fact]
    public async Task OpenReadAsync_throws_for_a_storage_key_that_does_not_exist()
    {
        var act = async () => await _storage.OpenReadAsync("zz/nonexistent.pdf", CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task OpenReadAsync_rejects_a_storage_key_that_tries_to_escape_the_root_directory()
    {
        var act = async () => await _storage.OpenReadAsync("../../../../windows/system32/config", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Two_uploads_with_the_same_original_file_name_get_different_storage_keys()
    {
        var first = await _storage.SaveAsync(new MemoryStream([1]), "igual.pdf", CancellationToken.None);
        var second = await _storage.SaveAsync(new MemoryStream([2]), "igual.pdf", CancellationToken.None);

        first.StorageKey.Should().NotBe(second.StorageKey);
    }
}
