namespace Vls.Shopflow.Catalog.Infrastructure.Seed;

public static class DemoClothingCatalogSeedData
{
    internal const string InventoryReason = "Carga inicial demo catálogo roupas";

    public static readonly string[] DemoProductSlugs =
    [
        "camiseta-basica-algodao",
        "camiseta-oversized",
        "camisa-social-manga-longa",
        "blusa-moletom-com-capuz",
        "jaqueta-jeans-feminina",
        "calca-jeans-masculina-reta",
        "calca-social-alfaiataria",
        "shorts-social-feminino-linho",
        "vestido-midi-manga-bufante",
        "saia-midi-evase"
    ];

    internal static readonly IReadOnlyList<DemoProductDefinition> Products =
    [
        new(
            Name: "Camiseta Básica Algodão",
            Slug: "camiseta-basica-algodao",
            CategoryName: "Camisetas",
            SkuBase: "CAM-BAS",
            RegularPrice: 69.90m,
            PromotionalPrice: 59.90m,
            LetterSizes: ["PP", "P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Branco", "camiseta-basica-branca.png"),
                new("Verde suave", "camiseta-basica-verde.png")
            ]),
        new(
            Name: "Camiseta Oversized",
            Slug: "camiseta-oversized",
            CategoryName: "Camisetas",
            SkuBase: "CAM-OVR",
            RegularPrice: 89.90m,
            PromotionalPrice: 79.90m,
            LetterSizes: ["P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Preto", "camiseta-oversize-preta.png"),
                new("Marrom", "camiseta-oversize-marrom.png")
            ]),
        new(
            Name: "Camisa Social Manga Longa",
            Slug: "camisa-social-manga-longa",
            CategoryName: "Camisas",
            SkuBase: "CAM-SOC",
            RegularPrice: 149.90m,
            PromotionalPrice: 129.90m,
            LetterSizes: ["P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Branco", "camisa-social-branca.png"),
                new("Off-white", "camisa-social-offwhite.png")
            ]),
        new(
            Name: "Blusa de Moletom com Capuz",
            Slug: "blusa-moletom-com-capuz",
            CategoryName: "Moletons",
            SkuBase: "MOL-CAP",
            RegularPrice: 179.90m,
            PromotionalPrice: 159.90m,
            LetterSizes: ["P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Cinza", "blusa-moletom-cinza.png"),
                new("Azul marinho", "blusa-moletom-azul-marinho.png")
            ]),
        new(
            Name: "Jaqueta Jeans Feminina",
            Slug: "jaqueta-jeans-feminina",
            CategoryName: "Jaquetas",
            SkuBase: "JAQ-JNS-FEM",
            RegularPrice: 229.90m,
            PromotionalPrice: 199.90m,
            LetterSizes: ["PP", "P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Azul jeans", "jaqueta jeans feminina.png", "jaqueta-jeans-feminina.png"),
                new("Jeans escuro", "jaqueta jeans escura feminina.png", "jaqueta-jeans-escura-feminina.png")
            ]),
        new(
            Name: "Calça Jeans Masculina Reta",
            Slug: "calca-jeans-masculina-reta",
            CategoryName: "Calças",
            SkuBase: "CAL-JNS-MAS",
            RegularPrice: 189.90m,
            PromotionalPrice: 169.90m,
            LetterSizes: null,
            NumericSizes: ["38", "40", "42", "44", "46"],
            Colors:
            [
                new("Azul jeans", "calca-jeans-masculina.png"),
                new("Jeans escuro", "calca-jeans-escura-masculina.png")
            ]),
        new(
            Name: "Calça Social Alfaiataria",
            Slug: "calca-social-alfaiataria",
            CategoryName: "Calças",
            SkuBase: "CAL-SOC",
            RegularPrice: 199.90m,
            PromotionalPrice: 179.90m,
            LetterSizes: null,
            NumericSizes: ["38", "40", "42", "44", "46"],
            Colors:
            [
                new("Cinza", "calca-social-cinza.png"),
                new("Cinza escuro", "calca-social-cinza-escuro.png")
            ]),
        new(
            Name: "Shorts Social Feminino de Linho",
            Slug: "shorts-social-feminino-linho",
            CategoryName: "Shorts",
            SkuBase: "SHO-LIN-FEM",
            RegularPrice: 119.90m,
            PromotionalPrice: 99.90m,
            LetterSizes: ["PP", "P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Off-white", "shorts-social-feminino-offwhite.png"),
                new("Verde oliva", "shorts-social-feminino-verde.png")
            ]),
        new(
            Name: "Vestido Midi Manga Bufante",
            Slug: "vestido-midi-manga-bufante",
            CategoryName: "Vestidos",
            SkuBase: "VES-MID-BUF",
            RegularPrice: 219.90m,
            PromotionalPrice: 189.90m,
            LetterSizes: ["PP", "P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Rosé", "vestido-feminino-rose.png"),
                new("Lilás", "vestido-feminino-lilas.png")
            ]),
        new(
            Name: "Saia Midi Evasê",
            Slug: "saia-midi-evase",
            CategoryName: "Saias",
            SkuBase: "SAI-MID-EVA",
            RegularPrice: 159.90m,
            PromotionalPrice: 139.90m,
            LetterSizes: ["PP", "P", "M", "G", "GG"],
            NumericSizes: null,
            Colors:
            [
                new("Off-white", "saia-feminina-offwhite.png"),
                new("Rosé", "saia-feminina-rose.png")
            ])
    ];

    internal static readonly IReadOnlyDictionary<string, string?> RequiredColorHex =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Branco"] = "#FFFFFF",
            ["Off-white"] = "#F8F5F2",
            ["Verde suave"] = "#BCE9D0",
            ["Verde oliva"] = "#6B7B45",
            ["Preto"] = "#000000",
            ["Marrom"] = "#7A4A21",
            ["Azul jeans"] = "#6A89A6",
            ["Jeans escuro"] = "#2D4A68",
            ["Azul marinho"] = "#0A2342",
            ["Cinza"] = "#A3A3A3",
            ["Cinza escuro"] = "#4B4B4B",
            ["Rosé"] = "#D8A7B1",
            ["Lilás"] = "#B497DD",
            ["Terracota"] = "#CB6D51"
        };

    internal static readonly string[] RequiredSizes =
    [
        "PP", "P", "M", "G", "GG", "38", "40", "42", "44", "46"
    ];

    internal sealed record DemoColorDefinition(
        string ColorName,
        string SourceFileName,
        string? PublicFileName = null);

    internal sealed record DemoProductDefinition(
        string Name,
        string Slug,
        string CategoryName,
        string SkuBase,
        decimal RegularPrice,
        decimal PromotionalPrice,
        string[]? LetterSizes,
        string[]? NumericSizes,
        DemoColorDefinition[] Colors);
}
