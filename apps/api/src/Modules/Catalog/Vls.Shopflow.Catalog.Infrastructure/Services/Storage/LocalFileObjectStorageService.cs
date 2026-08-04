using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services;

namespace Vls.Shopflow.Catalog.Infrastructure.Services.Storage;

/// <summary>Local disk under wwwroot/uploads (Development default).</summary>
public sealed class LocalFileObjectStorageService(
    IOptions<StorageOptions> storageOptions,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment hostEnvironment,
    ILogger<LocalFileObjectStorageService> logger) : IObjectStorageService
{
    public string ProviderName => StorageOptions.ProviderLocal;

    public async Task<ObjectStorageUploadResult> UploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken)
    {
        var physicalPath = ResolvePhysicalPath(request.ObjectKey);
        var dir = Path.GetDirectoryName(physicalPath)
                  ?? throw new InvalidOperationException("Invalid storage key.");
        Directory.CreateDirectory(dir);

        long size;
        await using (var fs = new FileStream(
                           physicalPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None,
                           65536,
                           useAsync: true))
        {
            await request.Content.CopyToAsync(fs, cancellationToken);
            size = fs.Length;
        }

        return new ObjectStorageUploadResult(
            request.ObjectKey,
            BuildPublicUrl(request.ObjectKey),
            request.ContentType,
            size);
    }

    public Task DeleteAsync(ObjectStorageDeleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var physicalPath = ResolvePhysicalPath(request.ObjectKey);
            if (File.Exists(physicalPath))
                File.Delete(physicalPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete local product image object {Key}", request.ObjectKey);
        }

        return Task.CompletedTask;
    }

    public string BuildPublicUrl(string objectKey)
    {
        var local = storageOptions.Value.Local;
        var publicBase = local.PublicBaseUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(publicBase))
        {
            var http = httpContextAccessor.HttpContext
                       ?? throw new InvalidOperationException(
                           "Storage:Local:PublicBaseUrl (or Uploads:PublicBaseUrl) must be set when HttpContext is unavailable.");
            publicBase = $"{http.Request.Scheme}://{http.Request.Host}".TrimEnd('/');
        }

        return ProductImageStorageKeys.BuildPublicUrl(publicBase, objectKey, prependUploadsSegment: true);
    }

    private string ResolvePhysicalPath(string key)
    {
        var rootPath = storageOptions.Value.Local.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
            rootPath = Path.Combine(hostEnvironment.ContentRootPath, "wwwroot", "uploads");

        var normalized = key.Replace('\\', '/').TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine(new[] { rootPath }.Concat(segments).ToArray());
    }
}
