using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Infrastructure.Repositories;
using Vls.Shopflow.Inventory.Infrastructure.Services;
using Vls.Shopflow.Inventory.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryModule(
        this IServiceCollection services,
        string connectionString,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.AddDbContext<InventoryDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "inventory");
            });

            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        RegisterServices(services);
        return services;
    }

    public static IServiceCollection AddInventoryModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> dbOptionsBuilder)
    {
        services.AddDbContext<InventoryDbContext>(dbOptionsBuilder);
        RegisterServices(services);
        return services;
    }

    public static IServiceCollection AddInventoryModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("Inventory")
                 ?? configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException("ConnectionStrings:Inventory or Catalog not configured.");
        return services.AddInventoryModule(cs, enableSensitiveLoggingOnDev);
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();
        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        services.AddScoped<IInventoryAtomicOperations, InventoryAtomicOperations>();
        services.AddScoped<IInventoryReadModel, InventoryReadModel>();
        services.AddScoped<IAdminInventorySkuReadModel, AdminInventorySkuReadModel>();
        services.AddScoped<IStockMovementReadModel, StockMovementReadModel>();
        services.AddScoped<ISkuExistenceChecker, CatalogSkuExistenceChecker>();
    }
}
