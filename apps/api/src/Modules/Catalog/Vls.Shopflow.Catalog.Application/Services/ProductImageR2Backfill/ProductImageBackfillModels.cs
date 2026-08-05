namespace Vls.Shopflow.Catalog.Application.Services.ProductImageR2Backfill;

public sealed record ProductImageBackfillOptions(
    string EnvironmentName,
    string SourceRoot,
    bool Execute,
    string? ConfirmPhrase,
    bool BackfillFlagEnabled,
    string StorageProvider,
    string R2Bucket,
    string R2PublicBaseUrl,
    string KeyPrefix,
    string? ConnectionString,
    string? ReportPath);

public sealed record ProductImageBackfillCandidate(
    Guid ImageId,
    Guid ProductId,
    string? ProductSlug,
    string? CurrentProvider,
    string? CurrentObjectKey,
    string CurrentUrl,
    string LocalRelativePath,
    string AbsoluteLocalPath,
    string PlannedObjectKey,
    string PlannedPublicUrl,
    string ContentType,
    long SizeBytes);

public enum ProductImageBackfillSkipReason
{
    AlreadyOnR2,
    MissingLocalFile,
    InvalidExtension,
    EmptyObjectKeyAndUrl,
    ProductMissing
}

public sealed record ProductImageBackfillSkipped(
    Guid ImageId,
    Guid ProductId,
    ProductImageBackfillSkipReason Reason,
    string Detail);

public sealed record ProductImageBackfillItemResult(
    Guid ImageId,
    Guid ProductId,
    string ObjectKey,
    string PublicUrl,
    bool Uploaded,
    bool DbUpdated,
    string? Error);

public sealed record ProductImageBackfillReport(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Mode,
    string EnvironmentName,
    string SourceRoot,
    string Bucket,
    string PublicBaseUrl,
    int TotalInDatabase,
    int Eligible,
    int AlreadyOnR2,
    int Skipped,
    int Uploaded,
    int Errors,
    int Unchanged,
    IReadOnlyList<ProductImageBackfillCandidate> Planned,
    IReadOnlyList<ProductImageBackfillSkipped> SkippedItems,
    IReadOnlyList<ProductImageBackfillItemResult> Results);
