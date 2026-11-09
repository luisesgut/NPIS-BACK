using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;

namespace NpiManagement.Infrastructure.FileStorage;

/// <summary>
/// Implementación local de IFileStorage para Development (ADR-021): guarda fuera del código
/// fuente, en un árbol de directorios "sharded" por los dos primeros caracteres del
/// StorageKey (evita miles de archivos en un solo directorio). El StorageKey es siempre
/// generado por el servidor (GUID), nunca a partir del nombre de archivo del cliente — evita
/// path traversal por diseño, sin necesidad de sanear una ruta ajena.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string sanitizedFileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(sanitizedFileName);
        var fileId = Guid.NewGuid().ToString("N");
        var shard = fileId[..2];
        var storageKey = $"{shard}/{fileId}{extension}";

        var fullPath = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        long sizeBytes;
        string sha256Hex;
        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            using var sha256 = SHA256.Create();
            await using var hashingStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write, leaveOpen: true);
            await content.CopyToAsync(hashingStream, cancellationToken);
            await hashingStream.FlushFinalBlockAsync(cancellationToken);
            sizeBytes = fileStream.Length;
            sha256Hex = Convert.ToHexString(sha256.Hash!);
        }

        return new StoredFile(storageKey, sizeBytes, sha256Hex);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(storageKey);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("El archivo referenciado ya no existe en el almacenamiento.", storageKey);

        return Task.FromResult<Stream>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    /// <summary>Resuelve StorageKey a una ruta física, verificando que quede dentro de la raíz configurada (defensa en profundidad).</summary>
    private string ResolvePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal) && fullPath != _rootPath)
            throw new InvalidOperationException("StorageKey resuelve fuera del directorio de almacenamiento permitido.");

        return fullPath;
    }
}
