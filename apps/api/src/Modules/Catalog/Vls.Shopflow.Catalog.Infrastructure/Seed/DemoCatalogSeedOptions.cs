namespace Vls.Shopflow.Catalog.Infrastructure.Seed;

public sealed class DemoCatalogSeedOptions
{
    public const string SectionName = "DemoCatalogSeed";

    public bool Enabled { get; set; }

    public bool CopyImages { get; set; } = true;

    public bool CreateInventory { get; set; } = true;

    public int DefaultStockQuantity { get; set; } = 20;
}
