namespace Vls.Shopflow.IdentityAccess.Infrastructure.Options;

public sealed class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    /// <summary>Idle timeout (sliding cookie lifetime). Default 30 minutes.</summary>
    public int IdleMinutes { get; set; } = 30;

    /// <summary>Hard cap from login, even with activity. Default 8 hours.</summary>
    public int AbsoluteHours { get; set; } = 8;

    /// <summary>Deprecated. Used only when <see cref="IdleMinutes"/> is unset/invalid.</summary>
    public int SessionHours { get; set; }

    public string CookieNameProduction { get; set; } = "__Host-shopflow_admin";
    public string CookieNameDevelopment { get; set; } = "shopflow_admin_dev";

    public TimeSpan GetIdleLifetime()
    {
        if (IdleMinutes > 0)
            return TimeSpan.FromMinutes(IdleMinutes);
        if (SessionHours > 0)
            return TimeSpan.FromHours(SessionHours);
        return TimeSpan.FromMinutes(30);
    }

    public TimeSpan GetAbsoluteLifetime()
    {
        var hours = AbsoluteHours > 0 ? AbsoluteHours : 8;
        var absolute = TimeSpan.FromHours(hours);
        var idle = GetIdleLifetime();
        return absolute < idle ? idle : absolute;
    }
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

/// <summary>
/// Optional demo Owner + Approved customer for local Development and TESTE.
/// Never applied in Production even if Enabled=true.
/// </summary>
public sealed class DemoUsersSeedOptions
{
    public const string SectionName = "DemoUsersSeed";

    public bool Enabled { get; set; }

    public bool ResetPasswords { get; set; }

    public string AdminEmail { get; set; } = "admin@teste.com.br";

    public string AdminPassword { get; set; } = "Shopflow@123";

    public string AdminName { get; set; } = "Admin Teste";

    public string CustomerEmail { get; set; } = "teste@teste.com.br";

    public string CustomerPassword { get; set; } = "Shopflow@123";

    public string CustomerName { get; set; } = "Cliente Teste";
}

public sealed class CustomerAuthOptions
{
    public const string SectionName = "CustomerAuth";

    /// <summary>Idle timeout for a normal (non-remember-me) session. Default 12 hours.</summary>
    public int SessionHours { get; set; } = 12;

    /// <summary>Hard cap from login for a normal session. Default 7 days.</summary>
    public int AbsoluteDays { get; set; } = 7;

    /// <summary>Persistent remember-me lifetime (idle + absolute). Capped at 14 days.</summary>
    public int RememberMeDays { get; set; } = 14;

    /// <summary>Deprecated. Used only when <see cref="SessionHours"/> is unset/invalid.</summary>
    public int SessionDays { get; set; }

    public string CookieNameProduction { get; set; } = "__Host-shopflow_customer";
    public string CookieNameDevelopment { get; set; } = "shopflow_customer_dev";

    public const int RememberMeDaysCap = 14;

    public TimeSpan GetIdleLifetime()
    {
        if (SessionHours > 0)
            return TimeSpan.FromHours(SessionHours);
        if (SessionDays > 0)
            return TimeSpan.FromDays(SessionDays);
        return TimeSpan.FromHours(12);
    }

    public TimeSpan GetAbsoluteLifetime()
    {
        var days = AbsoluteDays > 0 ? AbsoluteDays : 7;
        var absolute = TimeSpan.FromDays(days);
        var idle = GetIdleLifetime();
        return absolute < idle ? idle : absolute;
    }

    public TimeSpan GetRememberMeLifetime()
    {
        var days = RememberMeDays > 0 ? RememberMeDays : 14;
        if (days > RememberMeDaysCap)
            days = RememberMeDaysCap;
        return TimeSpan.FromDays(days);
    }
}
