namespace Vls.Shopflow.Catalog.Application.Options;

/// <summary>
/// Manual TEST-only backfill of local product images to Cloudflare R2.
/// Default false. Must never be enabled in Production.
/// </summary>
public sealed class R2ImageBackfillOptions
{
    public const string SectionName = "R2ImageBackfill";

    public const string ConfirmPhrase = "TESTE_R2_IMAGE_BACKFILL";

    public const string AllowedTestBucket = "shopflow-products-test";

    public const string AllowedTestPublicHost = "assets-teste.vipassessoriadigital.com.br";

    /// <summary>Must be true for --execute. Dry-run does not require it.</summary>
    public bool Enabled { get; set; }
}
