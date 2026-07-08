using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;
using Vls.Shopflow.IdentityAccess.Infrastructure.Middleware;
using Vls.Shopflow.IdentityAccess.Infrastructure.Options;
using Vls.Shopflow.IdentityAccess.Infrastructure.Services;

namespace Vls.Shopflow.IdentityAccess.Infrastructure;

public static class DependencyInjection
{
    public const string AdminLoginRateLimitPolicy = "admin-login";
    public const string CustomerLoginRateLimitPolicy = "customer-login";
    public const string CustomerRegisterRateLimitPolicy = "customer-register";
    public const string CustomerForgotPasswordRateLimitPolicy = "customer-forgot-password";
    public const string CustomerResetPasswordRateLimitPolicy = "customer-reset-password";
    public const string CorsPolicyName = "AllowFrontend";

    public static IServiceCollection AddIdentityAccessModule(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration,
        IHostEnvironment environment,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.AddDbContext<IdentityAccessDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString);
            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        RegisterServices(services, configuration, environment);
        return services;
    }

    public static IServiceCollection AddIdentityAccessModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> dbOptionsBuilder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<IdentityAccessDbContext>(dbOptionsBuilder);
        RegisterServices(services, configuration, environment);
        return services;
    }

    public static IServiceCollection AddIdentityAccessModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("IdentityAccess")
                 ?? configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException("ConnectionStrings:IdentityAccess or Catalog not configured.");
        return services.AddIdentityAccessModule(cs, configuration, environment, enableSensitiveLoggingOnDev);
    }

    public static IApplicationBuilder UseIdentityAccessMiddleware(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseMiddleware<CookiePrincipalMiddleware>();
        app.UseAuthorization();
        app.UseMiddleware<CsrfProtectionMiddleware>();
        return app;
    }

    private static void RegisterServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<AdminAuthOptions>(configuration.GetSection(AdminAuthOptions.SectionName));
        services.Configure<CustomerAuthOptions>(configuration.GetSection(CustomerAuthOptions.SectionName));
        services.Configure<ShopflowDataProtectionOptions>(configuration.GetSection(ShopflowDataProtectionOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));

        var dataProtectionPath = configuration[$"{ShopflowDataProtectionOptions.SectionName}:KeysPath"]
                                 ?? configuration["DataProtection:KeysPath"]
                                 ?? "./dataprotection-keys";
        Directory.CreateDirectory(dataProtectionPath);
        services.AddDataProtection()
            .SetApplicationName("Shopflow")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));

        var adminAuthOptions = configuration.GetSection(AdminAuthOptions.SectionName).Get<AdminAuthOptions>()
                               ?? new AdminAuthOptions();
        var customerAuthOptions = configuration.GetSection(CustomerAuthOptions.SectionName).Get<CustomerAuthOptions>()
                                  ?? new CustomerAuthOptions();

        var adminCookieName = environment.IsDevelopment()
            ? adminAuthOptions.CookieNameDevelopment
            : adminAuthOptions.CookieNameProduction;
        var customerCookieName = environment.IsDevelopment()
            ? customerAuthOptions.CookieNameDevelopment
            : customerAuthOptions.CookieNameProduction;

        services.AddIdentity<ShopflowUser, ShopflowRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<IdentityAccessDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserClaimsPrincipalFactory<ShopflowUser>, ShopflowUserClaimsPrincipalFactory>();

        var securePolicy = environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = adminCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(adminAuthOptions.SessionHours);
            options.LoginPath = "/api/auth/admin/login";
            options.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = AuthSchemes.AdminCookie;
                options.DefaultAuthenticateScheme = AuthSchemes.AdminCookie;
                options.DefaultSignInScheme = AuthSchemes.AdminCookie;
                options.DefaultChallengeScheme = AuthSchemes.AdminCookie;
            })
            .AddCookie(AuthSchemes.CustomerCookie, options =>
            {
                options.Cookie.Name = customerCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = securePolicy;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(customerAuthOptions.SessionDays);
                options.LoginPath = "/api/auth/customer/login";
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.Backoffice, policy =>
            {
                policy.AddAuthenticationSchemes(AuthSchemes.AdminCookie);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(AuthRoles.Owner);
                policy.RequireAssertion(ctx =>
                {
                    var isStaff = ctx.User.FindFirst(AuthClaims.IsStaff)?.Value;
                    return string.Equals(isStaff, "true", StringComparison.OrdinalIgnoreCase);
                });
            });

            options.AddPolicy(AuthPolicies.Customer, policy =>
            {
                policy.AddAuthenticationSchemes(AuthSchemes.CustomerCookie);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(AuthRoles.Customer);
                policy.RequireAssertion(ctx =>
                {
                    var isCustomer = ctx.User.FindFirst(AuthClaims.IsCustomer)?.Value;
                    return string.Equals(isCustomer, "true", StringComparison.OrdinalIgnoreCase);
                });
            });
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = environment.IsDevelopment() ? "shopflow_csrf_dev" : "__Host-shopflow_csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = securePolicy;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AdminLoginRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10,
                        QueueLimit = 0
                    }));

            options.AddPolicy(CustomerLoginRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10,
                        QueueLimit = 0
                    }));

            options.AddPolicy(CustomerRegisterRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = environment.IsDevelopment() ? 100 : 5,
                        QueueLimit = 0
                    }));

            options.AddPolicy(CustomerForgotPasswordRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = environment.IsDevelopment() ? 100 : 5,
                        QueueLimit = 0
                    }));

            options.AddPolicy(CustomerResetPasswordRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = environment.IsDevelopment() ? 100 : 5,
                        QueueLimit = 0
                    }));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<IIdentityEmailSender, LoggingIdentityEmailSender>();
        services.AddScoped<IAdminLoginService, AdminLoginService>();
        services.AddScoped<IAdminSignInService, AdminSignInService>();
        services.AddScoped<ICurrentAdminAccessor, CurrentAdminAccessor>();
        services.AddScoped<ICustomerRegistrationService, CustomerRegistrationService>();
        services.AddScoped<ICustomerLoginService, CustomerLoginService>();
        services.AddScoped<ICustomerSignInService, CustomerSignInService>();
        services.AddScoped<ICurrentCustomerAccessor, CurrentCustomerAccessor>();
        services.AddScoped<ICustomerPasswordService, CustomerPasswordService>();
    }

}
