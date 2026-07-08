namespace Vls.Shopflow.IdentityAccess.Infrastructure.Options;

public sealed class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    public int SessionHours { get; set; } = 8;
    public string CookieNameProduction { get; set; } = "__Host-shopflow_admin";
    public string CookieNameDevelopment { get; set; } = "shopflow_admin_dev";
}

public sealed class ShopflowCorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class ShopflowDataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public string KeysPath { get; set; } = "./dataprotection-keys";
}

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Name { get; set; }
}

public sealed class CustomerAuthOptions
{
    public const string SectionName = "CustomerAuth";

    public int SessionDays { get; set; } = 30;
    public string CookieNameProduction { get; set; } = "__Host-shopflow_customer";
    public string CookieNameDevelopment { get; set; } = "shopflow_customer_dev";
}
