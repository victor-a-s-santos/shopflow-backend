using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Vls.Shopflow.Catalog.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Infrastructure.Services.Storage;

public sealed class LocalImageStorage(
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment hostEnvironment) : IImageStorage
{
    public async Task<StoredImage> SaveAsync(
        Guid productId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var rootPath = configuration["Uploads:RootPath"];
        if (string.IsNullOrWhiteSpace(rootPath))
            rootPath = Path.Combine(hostEnvironment.ContentRootPath, "wwwroot", "uploads");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/jpeg" or "image/jpg" => ".jpg",
                _ => ".bin"
            };

        var safeExt = ext.Length > 10 ? ".bin" : ext;
        var uniqueName = $"{Guid.NewGuid():N}{safeExt}";
        var relativeDir = Path.Combine("products", productId.ToString());
        var physicalDir = Path.Combine(rootPath, relativeDir);
        Directory.CreateDirectory(physicalDir);

        var physicalPath = Path.Combine(physicalDir, uniqueName);
        await using (var fs = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
        {
            await content.CopyToAsync(fs, cancellationToken);
        }

        var publicBase = configuration["Uploads:PublicBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(publicBase))
        {
            var http = httpContextAccessor.HttpContext;
            if (http is null)
                throw new InvalidOperationException("Uploads:PublicBaseUrl must be set when HttpContext is not available.");

            publicBase = $"{http.Request.Scheme}://{http.Request.Host}".TrimEnd('/');
        }

        var urlPath = $"/uploads/products/{productId}/{uniqueName}".Replace('\\', '/');
        var publicUrl = $"{publicBase}{urlPath}";

        var storagePath = Path.Combine(relativeDir, uniqueName).Replace('\\', '/');

        return new StoredImage(publicUrl, storagePath);
    }
}
