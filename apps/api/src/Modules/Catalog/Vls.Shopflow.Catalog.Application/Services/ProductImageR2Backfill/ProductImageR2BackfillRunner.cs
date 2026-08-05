using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services;

namespace Vls.Shopflow.Catalog.Application.Services.ProductImageR2Backfill;

public interface IProductImageBackfillStore
{
    Task<IReadOnlyList<ProductImageBackfillRow>> LoadAllAsync(CancellationToken cancellationToken);

    Task PersistMigrationAsync(
        Guid imageId,
        string publicUrl,
        string objectKey,
        string storageProvider,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken);
}

public sealed record ProductImageBackfillRow(
    Guid ImageId,
    Guid ProductId,
    string? ProductSlug,
    string Url,
    string? ObjectKey,
    string? StorageProvider,
    string? ContentType,
    long? SizeBytes);

/// <summary>
/// Pure orchestration for TEST-only local→R2 backfill. No EF/DI coupling in unit tests.
/// </summary>
public sealed class ProductImageR2BackfillRunner(
    IProductImageBackfillStore store,
    IObjectStorageService objectStorage)
{
    public async Task<ProductImageBackfillReport> RunAsync(
        ProductImageBackfillOptions options,
        CancellationToken cancellationToken = default)
    {
        ProductImageBackfillGuards.EnsureSafeToRun(options);

        var started = DateTimeOffset.UtcNow;
        var rows = await store.LoadAllAsync(cancellationToken);

        var planned = new List<ProductImageBackfillCandidate>();
        var skipped = new List<ProductImageBackfillSkipped>();
        var alreadyOnR2 = 0;

        foreach (var row in rows)
        {
            if (await ShouldSkipAsCompleteR2Async(row, options, cancellationToken))
            {
                alreadyOnR2++;
                skipped.Add(new ProductImageBackfillSkipped(
                    row.ImageId,
                    row.ProductId,
                    ProductImageBackfillSkipReason.AlreadyOnR2,
                    "StorageProvider=CloudflareR2, key/url valid, object exists"));
                continue;
            }

            if (!ProductImageBackfillSelector.IsEligibleProvider(row.StorageProvider))
            {
                skipped.Add(new ProductImageBackfillSkipped(
                    row.ImageId,
                    row.ProductId,
                    ProductImageBackfillSkipReason.AlreadyOnR2,
                    $"Unsupported provider '{row.StorageProvider}'"));
                continue;
            }

            var relative = ProductImageBackfillSelector.TryResolveLocalRelativePath(
                row.ObjectKey,
                row.Url);

            if (string.IsNullOrWhiteSpace(relative))
            {
                skipped.Add(new ProductImageBackfillSkipped(
                    row.ImageId,
                    row.ProductId,
                    ProductImageBackfillSkipReason.EmptyObjectKeyAndUrl,
                    "Cannot resolve local relative path from ObjectKey/Url"));
                continue;
            }

            if (!ProductImageBackfillSelector.IsAllowedExtension(relative))
            {
                skipped.Add(new ProductImageBackfillSkipped(
                    row.ImageId,
                    row.ProductId,
                    ProductImageBackfillSkipReason.InvalidExtension,
                    relative));
                continue;
            }

            var absolute = Path.GetFullPath(
                Path.Combine(options.SourceRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(Path.GetFullPath(options.SourceRoot), StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(new ProductImageBackfillSkipped(
                    row.ImageId,
                    row.ProductId,
                    ProductImageBackfillSkipReason.MissingLocalFile,
                    "Path escapes source root"));
                continue;
            }

            if (!File.Exists(absolute))
            {
                skipped.Add(new ProductImageBackfillSkipped(
                    row.ImageId,
                    row.ProductId,
                    ProductImageBackfillSkipReason.MissingLocalFile,
                    absolute));
                continue;
            }

            var plannedKey = ProductImageBackfillSelector.BuildPlannedObjectKey(
                options.KeyPrefix,
                row.ProductId,
                row.ImageId,
                row.ProductSlug,
                relative);

            var publicUrl = ProductImageStorageKeys.BuildPublicUrl(
                options.R2PublicBaseUrl,
                plannedKey,
                prependUploadsSegment: false);

            var size = new FileInfo(absolute).Length;
            var contentType = string.IsNullOrWhiteSpace(row.ContentType)
                ? ProductImageBackfillSelector.GuessContentType(relative)
                : row.ContentType!;

            planned.Add(new ProductImageBackfillCandidate(
                row.ImageId,
                row.ProductId,
                row.ProductSlug,
                row.StorageProvider,
                row.ObjectKey,
                row.Url,
                relative,
                absolute,
                plannedKey,
                publicUrl,
                contentType,
                size));
        }

        var results = new List<ProductImageBackfillItemResult>();
        var uploaded = 0;
        var errors = 0;
        var metadataOnly = 0;

        if (options.Execute)
        {
            foreach (var item in planned)
            {
                try
                {
                    var didUpload = false;
                    ObjectStorageUploadResult upload;
                    if (await objectStorage.ExistsAsync(item.PlannedObjectKey, cancellationToken))
                    {
                        upload = new ObjectStorageUploadResult(
                            item.PlannedObjectKey,
                            item.PlannedPublicUrl,
                            item.ContentType,
                            item.SizeBytes);
                        metadataOnly++;
                    }
                    else
                    {
                        await using var fs = new FileStream(
                            item.AbsoluteLocalPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            65536,
                            useAsync: true);

                        upload = await objectStorage.UploadAsync(
                            new ObjectStorageUploadRequest(
                                item.PlannedObjectKey,
                                fs,
                                item.ContentType,
                                R2StorageOptions.ImageCacheControl),
                            cancellationToken);
                        didUpload = true;
                        uploaded++;
                    }

                    await store.PersistMigrationAsync(
                        item.ImageId,
                        upload.PublicUrl,
                        upload.ObjectKey,
                        StorageOptions.ProviderCloudflareR2,
                        upload.ContentType,
                        upload.SizeBytes,
                        cancellationToken);

                    results.Add(new ProductImageBackfillItemResult(
                        item.ImageId,
                        item.ProductId,
                        upload.ObjectKey,
                        upload.PublicUrl,
                        Uploaded: didUpload,
                        DbUpdated: true,
                        Error: null));
                }
                catch (Exception ex)
                {
                    errors++;
                    results.Add(new ProductImageBackfillItemResult(
                        item.ImageId,
                        item.ProductId,
                        item.PlannedObjectKey,
                        item.PlannedPublicUrl,
                        Uploaded: false,
                        DbUpdated: false,
                        Error: ex.Message));
                }
            }
        }

        var finished = DateTimeOffset.UtcNow;
        var unchanged = alreadyOnR2 + metadataOnly;
        return new ProductImageBackfillReport(
            started,
            finished,
            options.Execute ? "execute" : "dry-run",
            options.EnvironmentName,
            options.SourceRoot,
            options.R2Bucket,
            options.R2PublicBaseUrl,
            rows.Count,
            planned.Count,
            alreadyOnR2,
            skipped.Count,
            uploaded,
            errors,
            unchanged,
            planned,
            skipped,
            results);
    }

    private async Task<bool> ShouldSkipAsCompleteR2Async(
        ProductImageBackfillRow row,
        ProductImageBackfillOptions options,
        CancellationToken cancellationToken)
    {
        if (!ProductImageBackfillSelector.IsAlreadyOnR2(row.StorageProvider))
            return false;

        if (string.IsNullOrWhiteSpace(row.ObjectKey)
            || DemoSeedR2MigrationRules.IsLegacySeedObjectKey(row.ObjectKey)
            || !DemoSeedR2MigrationRules.UrlMatchesR2PublicBase(row.Url, options.R2PublicBaseUrl))
            return false;

        return await objectStorage.ExistsAsync(row.ObjectKey, cancellationToken);
    }
}

public static class ProductImageBackfillReportWriter
{
    public static string FormatMarkdown(ProductImageBackfillReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# R2 TEST product images backfill report");
        sb.AppendLine();
        sb.AppendLine($"- Started: `{report.StartedAt:O}`");
        sb.AppendLine($"- Finished: `{report.FinishedAt:O}`");
        sb.AppendLine($"- Mode: **{report.Mode}**");
        sb.AppendLine($"- Environment: `{report.EnvironmentName}`");
        sb.AppendLine($"- Source root: `{report.SourceRoot}`");
        sb.AppendLine($"- Bucket: `{report.Bucket}`");
        sb.AppendLine($"- PublicBaseUrl: `{report.PublicBaseUrl}`");
        sb.AppendLine();
        sb.AppendLine("## Totals");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Count |");
        sb.AppendLine($"|--------|------:|");
        sb.AppendLine($"| In database | {report.TotalInDatabase} |");
        sb.AppendLine($"| Eligible | {report.Eligible} |");
        sb.AppendLine($"| Already on R2 | {report.AlreadyOnR2} |");
        sb.AppendLine($"| Skipped | {report.Skipped} |");
        sb.AppendLine($"| Uploaded | {report.Uploaded} |");
        sb.AppendLine($"| Unchanged | {report.Unchanged} |");
        sb.AppendLine($"| Errors / failed | {report.Errors} |");
        sb.AppendLine();

        if (report.Planned.Count > 0)
        {
            sb.AppendLine("## Planned / eligible (sample up to 50)");
            sb.AppendLine();
            sb.AppendLine("| ImageId | ProductId | Local path | Object key |");
            sb.AppendLine("|---------|-----------|------------|------------|");
            foreach (var p in report.Planned.Take(50))
            {
                sb.AppendLine(
                    $"| `{p.ImageId}` | `{p.ProductId}` | `{p.LocalRelativePath}` | `{p.PlannedObjectKey}` |");
            }

            sb.AppendLine();
        }

        if (report.SkippedItems.Count > 0)
        {
            sb.AppendLine("## Skipped (sample up to 50)");
            sb.AppendLine();
            sb.AppendLine("| ImageId | ProductId | Reason | Detail |");
            sb.AppendLine("|---------|-----------|--------|--------|");
            foreach (var s in report.SkippedItems.Take(50))
            {
                sb.AppendLine(
                    $"| `{s.ImageId}` | `{s.ProductId}` | `{s.Reason}` | {SanitizeDetail(s.Detail)} |");
            }

            sb.AppendLine();
        }

        if (report.Results.Count > 0)
        {
            sb.AppendLine("## Execute results (sample up to 50)");
            sb.AppendLine();
            sb.AppendLine("| ImageId | Uploaded | DbUpdated | Error |");
            sb.AppendLine("|---------|----------|-----------|-------|");
            foreach (var r in report.Results.Take(50))
            {
                sb.AppendLine(
                    $"| `{r.ImageId}` | {r.Uploaded} | {r.DbUpdated} | {SanitizeDetail(r.Error)} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Notes");
        sb.AppendLine();
        sb.AppendLine("- Secrets (AccessKey/Secret) are never written to this report.");
        sb.AppendLine("- Local files are **not** deleted by this tool.");
        sb.AppendLine("- Only Testing + shopflow-products-test is allowed.");
        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine();
        if (report.Mode == "dry-run")
        {
            sb.AppendLine("1. Review eligible rows.");
            sb.AppendLine("2. Set `R2ImageBackfill__Enabled=true` on TEST only.");
            sb.AppendLine($"3. Re-run with `--execute --confirm {R2ImageBackfillOptions.ConfirmPhrase}`.");
        }
        else
        {
            sb.AppendLine("1. Spot-check public URLs on the TEST storefront.");
            sb.AppendLine("2. Verify objects in the R2 test bucket.");
            sb.AppendLine("3. Keep local files until validation is complete.");
            sb.AppendLine("4. Set `R2ImageBackfill__Enabled=false` again.");
        }

        var text = sb.ToString();
        AssertNoSecrets(text);
        return text;
    }

    public static void AssertNoSecrets(string reportText)
    {
        string[] forbidden =
        [
            "SecretAccessKey",
            "AccessKeyId=",
            "AWS_SECRET",
            "R2_SECRET"
        ];

        foreach (var token in forbidden)
        {
            if (reportText.Contains(token, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Report must not contain secrets.");
        }
    }

    private static string SanitizeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return "";
        return detail.Replace("|", "/", StringComparison.Ordinal).Trim();
    }
}
