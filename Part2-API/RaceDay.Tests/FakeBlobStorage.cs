using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>In-memory stand-in for Azure Blob Storage, so the tests need no Azure account.</summary>
public class FakeBlobStorage : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _blobs = new();

    public bool IsConfigured => true;

    public async Task<string> UploadAsync(Stream content, string contentType, string folder, string extension, CancellationToken ct = default)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, ct);
        var name = $"{folder}/{Guid.NewGuid():N}{extension}";
        _blobs[name] = (memory.ToArray(), contentType);
        return name;
    }

    public Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        if (!_blobs.TryGetValue(blobName, out var blob)) return Task.FromResult<BlobFile?>(null);
        return Task.FromResult<BlobFile?>(new BlobFile(new MemoryStream(blob.Bytes), blob.ContentType));
    }

    public Task DeleteAsync(string? blobName, CancellationToken ct = default)
    {
        if (blobName is not null) _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
}
