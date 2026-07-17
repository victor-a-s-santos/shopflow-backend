using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Infrastructure.Repositories;
using Vls.Shopflow.Catalog.Infrastructure.Services;
using Vls.Shopflow.Catalog.Infrastructure.Services.Storage;
using Vls.Shopflow.Catalog.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.Catalog.Infrastructure;

public static class DependencyInjection
{
    
    /// <summary>
    /// Registro completo do módulo Catalog usando connection string (Npgsql).
    /// </summary>
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        string connectionString,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.AddDbContext<CatalogDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                // se quiser separar migrations por assembly:
                // npg.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
            });

            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        RegisterServices(services);
        return services;
    }

    /// <summary>
    /// Versão flexível: você controla o DbContextOptions (ex.: InMemory para testes).
    /// </summary>
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> dbOptionsBuilder)
    {
        services.AddDbContext<CatalogDbContext>(dbOptionsBuilder);
        RegisterServices(services);
        return services;
    }

    /// <summary>
    /// Conveniência: lê ConnectionStrings:Catalog do IConfiguration.
    /// </summary>
    public static IServiceCollection AddCatalogModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException("ConnectionStrings:Catalog não configurado.");
        return services.AddCatalogModule(cs, enableSensitiveLoggingOnDev);
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // UoW específica do módulo
        services.AddScoped<ICatalogUnitOfWork, CatalogUnitOfWork>();

        // Repositórios / Read models
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReadModel, ProductReadModel>();
        services.AddScoped<IAttributeDefinitionReadModel, AttributeDefinitionReadModel>();
        services.AddScoped<IAttributeDefinitionLookup, AttributeDefinitionLookup>();
        services.AddScoped<ICategoryReadModel, CategoryReadModel>();
        services.AddScoped<ISkuLifecycleGuard, SkuLifecycleGuard>();

        services.AddScoped<ISlugService, SlugService>();

        services.AddHttpContextAccessor();
        services.AddScoped<IImageStorage, LocalImageStorage>();
    }
}
