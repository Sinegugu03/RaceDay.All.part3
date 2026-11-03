using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

public sealed record BlobFile(Stream Content, string ContentType);

/// <summary>
/// All image storage goes through this service, so the MVC front end never talks to Azure directly.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>False when no Azure Storage connection string has been configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads an image and returns the new blob name.</summary>
    Task<string> UploadAsync(Stream content, string contentType, string folder, string extension, CancellationToken ct = default);

    /// <summary>Opens a blob for reading, or returns null when it does not exist.</summary>
    Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default);

    Task DeleteAsync(string? blobName, CancellationToken ct = default);
}

/// <summary>Azure Blob Storage implementation (works with a real storage account or the Azurite emulator).</summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient? _container;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _containerReady;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["BlobStorage:ConnectionString"];
        var containerName = configuration["BlobStorage:ContainerName"];
        if (string.IsNullOrWhiteSpace(containerName)) containerName = "raceday-images";

        if (!string.IsNullOrWhiteSpace(connectionString))
            _container = new BlobContainerClient(connectionString, containerName);
    }

    public bool IsConfigured => _container is not null;

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        if (_container is null) throw new InvalidOperationException("Azure Blob Storage is not configured.");

        if (!_containerReady)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!_containerReady)
                {
                    // Private container: images are only served through the API.
                    await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                    _containerReady = true;
                }
            }
            finally
            {
                _lock.Release();
            }
        }
        return _container;
    }

    public async Task<string> UploadAsync(Stream content, string contentType, string folder, string extension, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
        return blobName;
    }

    public async Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blob = container.GetBlobClient(blobName);

        var exists = await blob.ExistsAsync(ct);
        if (!exists.Value) return null;

        var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return new BlobFile(download.Value.Content, download.Value.Details.ContentType);
    }

    public async Task DeleteAsync(string? blobName, CancellationToken ct = default)
    {
        if (_container is null || string.IsNullOrWhiteSpace(blobName)) return;
        var container = await GetContainerAsync(ct);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }
}
