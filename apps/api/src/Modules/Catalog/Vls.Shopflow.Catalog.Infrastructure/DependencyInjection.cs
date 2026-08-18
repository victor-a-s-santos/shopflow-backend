using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Infrastructure.Repositories;
using Vls.Shopflow.Catalog.Infrastructure.Services;
using Vls.Shopflow.Catalog.Infrastructure.Services.Storage;
using Vls.Shopflow.Catalog.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.AddDbContext<CatalogDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString);

            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        RegisterServices(services, configuration);
        return services;
    }

    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> dbOptionsBuilder,
        IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(dbOptionsBuilder);
        RegisterServices(services, configuration);
        return services;
    }

    public static IServiceCollection AddCatalogModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException("ConnectionStrings:Catalog não configurado.");
        return services.AddCatalogModule(cs, configuration, enableSensitiveLoggingOnDev);
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICatalogUnitOfWork, CatalogUnitOfWork>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReadModel, ProductReadModel>();
        services.AddScoped<IAdminProductReadModel, AdminProductReadModel>();
        services.AddScoped<IAttributeDefinitionReadModel, AttributeDefinitionReadModel>();
        services.AddScoped<IAttributeDefinitionLookup, AttributeDefinitionLookup>();
        services.AddScoped<ICategoryReadModel, CategoryReadModel>();
        services.AddScoped<ISkuLifecycleGuard, SkuLifecycleGuard>();

        services.AddScoped<ISlugService, SlugService>();

        services.Configure<StorageOptions>(opts =>
        {
            configuration.GetSection(StorageOptions.SectionName).Bind(opts);

            // Backward-compat: Uploads:* → Storage:Local when Local public URL empty.
            if (string.IsNullOrWhiteSpace(opts.Local.PublicBaseUrl))
                opts.Local.PublicBaseUrl = configuration["Uploads:PublicBaseUrl"] ?? "";
            if (string.IsNullOrWhiteSpace(opts.Local.RootPath))
                opts.Local.RootPath = configuration["Uploads:RootPath"] ?? "";

            // Backward-compat: flat R2Storage:* → Storage when Provider still Local and R2Storage enabled.
            var legacy = configuration.GetSection("R2Storage");
            if (legacy.Exists()
                && string.Equals(legacy["Enabled"], "true", StringComparison.OrdinalIgnoreCase)
                && string.Equals(legacy["Provider"], StorageOptions.ProviderCloudflareR2, StringComparison.OrdinalIgnoreCase)
                && !opts.UseCloudflareR2)
            {
                opts.Provider = StorageOptions.ProviderCloudflareR2;
                opts.R2.AccountId = legacy["AccountId"] ?? opts.R2.AccountId;
                opts.R2.Bucket = legacy["BucketName"] ?? legacy["Bucket"] ?? opts.R2.Bucket;
                opts.R2.AccessKeyId = legacy["AccessKeyId"] ?? opts.R2.AccessKeyId;
                opts.R2.SecretAccessKey = legacy["SecretAccessKey"] ?? opts.R2.SecretAccessKey;
                opts.R2.Endpoint = legacy["ServiceUrl"] ?? legacy["Endpoint"] ?? opts.R2.Endpoint;
                opts.R2.PublicBaseUrl = legacy["PublicBaseUrl"] ?? opts.R2.PublicBaseUrl;
                opts.R2.KeyPrefix = legacy["ProductImagesPrefix"] ?? legacy["KeyPrefix"] ?? opts.R2.KeyPrefix;
            }
        });

        services.AddHttpContextAccessor();

        var storage = new StorageOptions();
        configuration.GetSection(StorageOptions.SectionName).Bind(storage);
        // Apply same legacy merge for registration decision
        var legacyEnabled = string.Equals(
            configuration["R2Storage:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        var useR2 = storage.UseCloudflareR2
                    || (legacyEnabled
                        && string.Equals(
                            configuration["R2Storage:Provider"],
                            StorageOptions.ProviderCloudflareR2,
                            StringComparison.OrdinalIgnoreCase));

        if (useR2)
            services.AddSingleton<IObjectStorageService, CloudflareR2ObjectStorageService>();
        else
            services.AddSingleton<IObjectStorageService, LocalFileObjectStorageService>();

        services.AddScoped<IImageStorage, ProductImageStorageService>();
    }
}
