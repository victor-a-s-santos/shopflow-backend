using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services;

namespace Vls.Shopflow.Catalog.Infrastructure.Services.Storage;

/// <summary>Cloudflare R2 via S3-compatible API.</summary>
public sealed class CloudflareR2ObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly StorageOptions _options;
    private readonly IAmazonS3 _s3;
    private readonly ILogger<CloudflareR2ObjectStorageService> _logger;
    private readonly bool _ownsClient;

    public CloudflareR2ObjectStorageService(
        IOptions<StorageOptions> options,
        ILogger<CloudflareR2ObjectStorageService> logger)
        : this(options, logger, client: null)
    {
    }

    internal CloudflareR2ObjectStorageService(
        IOptions<StorageOptions> options,
        ILogger<CloudflareR2ObjectStorageService> logger,
        IAmazonS3? client)
    {
        _options = options.Value;
        _logger = logger;
        ValidateOptions(_options);

        if (client is not null)
        {
            _s3 = client;
            _ownsClient = false;
        }
        else
        {
            _s3 = CreateClient(_options);
            _ownsClient = true;
        }
    }

    public string ProviderName => StorageOptions.ProviderCloudflareR2;

    public async Task<ObjectStorageUploadResult> UploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken)
    {
        long sizeBytes = 0;
        Stream uploadStream = request.Content;
        MemoryStream? buffer = null;

        try
        {
            if (request.Content.CanSeek)
            {
                sizeBytes = Math.Max(0, request.Content.Length - request.Content.Position);
            }
            else
            {
                buffer = new MemoryStream();
                await request.Content.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;
                sizeBytes = buffer.Length;
                uploadStream = buffer;
            }

            var put = new PutObjectRequest
            {
                BucketName = _options.R2.Bucket,
                Key = request.ObjectKey,
                InputStream = uploadStream,
                ContentType = request.ContentType,
                Headers =
                {
                    CacheControl = request.CacheControl ?? R2StorageOptions.ImageCacheControl
                },
                DisablePayloadSigning = true,
                AutoCloseStream = false
            };

            await _s3.PutObjectAsync(put, cancellationToken);

            return new ObjectStorageUploadResult(
                request.ObjectKey,
                BuildPublicUrl(request.ObjectKey),
                request.ContentType,
                sizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "R2 upload failed for key {Key}", request.ObjectKey);
            throw new InvalidOperationException("Falha ao enviar a imagem para o armazenamento.", ex);
        }
        finally
        {
            if (buffer is not null)
                await buffer.DisposeAsync();
        }
    }

    public async Task DeleteAsync(ObjectStorageDeleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _s3.DeleteObjectAsync(_options.R2.Bucket, request.ObjectKey, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound
            || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(ex, "R2 object already missing for key {Key}", request.ObjectKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "R2 delete failed for key {Key}", request.ObjectKey);
        }
    }

    public string BuildPublicUrl(string objectKey)
        => ProductImageStorageKeys.BuildPublicUrl(_options.R2.PublicBaseUrl, objectKey, prependUploadsSegment: false);

    public void Dispose()
    {
        if (_ownsClient)
            _s3.Dispose();
    }

    private static void ValidateOptions(StorageOptions options)
    {
        var r2 = options.R2;
        if (string.IsNullOrWhiteSpace(r2.Bucket))
            throw new InvalidOperationException("Storage:R2:Bucket is required when Provider=CloudflareR2.");
        if (string.IsNullOrWhiteSpace(r2.AccessKeyId) || string.IsNullOrWhiteSpace(r2.SecretAccessKey))
            throw new InvalidOperationException("Storage:R2:AccessKeyId and SecretAccessKey are required when Provider=CloudflareR2.");
        if (string.IsNullOrWhiteSpace(r2.Endpoint))
            throw new InvalidOperationException("Storage:R2:Endpoint is required when Provider=CloudflareR2.");
        if (string.IsNullOrWhiteSpace(r2.PublicBaseUrl))
            throw new InvalidOperationException("Storage:R2:PublicBaseUrl is required when Provider=CloudflareR2.");
    }

    private static IAmazonS3 CreateClient(StorageOptions options)
    {
        var r2 = options.R2;
        var config = new AmazonS3Config
        {
            ServiceURL = r2.Endpoint.TrimEnd('/'),
            ForcePathStyle = r2.ForcePathStyle,
            AuthenticationRegion = string.IsNullOrWhiteSpace(r2.Region) ? "auto" : r2.Region
        };

        var credentials = new BasicAWSCredentials(r2.AccessKeyId, r2.SecretAccessKey);
        return new AmazonS3Client(credentials, config);
    }
}
