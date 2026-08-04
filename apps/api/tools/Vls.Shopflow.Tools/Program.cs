using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services.ProductImageR2Backfill;
using Vls.Shopflow.Catalog.Infrastructure;
using Vls.Shopflow.Catalog.Infrastructure.Services.Storage;

namespace Vls.Shopflow.Tools;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args is ["product-images", "backfill-r2", ..])
            return await RunProductImagesBackfillR2Async(args.Skip(2).ToArray());

        Console.Error.WriteLine($"Unknown command: {string.Join(' ', args)}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Vls.Shopflow.Tools — maintenance CLI (TEST-safe)

            product-images backfill-r2
              Manual backfill of local product images → Cloudflare R2 (TEST only).

              Dry-run:
                dotnet run --project tools/Vls.Shopflow.Tools -- product-images backfill-r2 \
                  --environment Testing \
                  --source-root /app/wwwroot/uploads \
                  --dry-run

              Execute:
                R2ImageBackfill__Enabled=true \
                dotnet run --project tools/Vls.Shopflow.Tools -- product-images backfill-r2 \
                  --environment Testing \
                  --source-root /app/wwwroot/uploads \
                  --execute \
                  --confirm TESTE_R2_IMAGE_BACKFILL

            Forbidden in Production. Does not delete local files.
            """);
    }

    private static async Task<int> RunProductImagesBackfillR2Async(string[] args)
    {
        var cli = ParseBackfillArgs(args);
        var environmentName = cli.Environment
                              ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                              ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                              ?? "Development";

        if (ProductImageBackfillGuards.IsProduction(environmentName)
            || ProductImageBackfillGuards.IsProduction(cli.Environment))
        {
            Console.Error.WriteLine("ABORT: Production is forbidden for product-images backfill-r2.");
            return 1;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var storage = new StorageOptions();
        config.GetSection(StorageOptions.SectionName).Bind(storage);
        if (string.IsNullOrWhiteSpace(storage.Local.PublicBaseUrl))
            storage.Local.PublicBaseUrl = config["Uploads:PublicBaseUrl"] ?? "";
        if (string.IsNullOrWhiteSpace(storage.Local.RootPath))
            storage.Local.RootPath = config["Uploads:RootPath"] ?? "";

        var backfillFlag = config.GetSection(R2ImageBackfillOptions.SectionName).Get<R2ImageBackfillOptions>()
                           ?? new R2ImageBackfillOptions();

        var connectionString = config.GetConnectionString("Catalog")
                               ?? config["ConnectionStrings__Catalog"]
                               ?? throw new InvalidOperationException("ConnectionStrings:Catalog is required.");

        var sourceRoot = cli.SourceRoot
                         ?? storage.Local.RootPath
                         ?? config["Uploads:RootPath"]
                         ?? throw new InvalidOperationException("--source-root is required.");

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }));
        services.AddSingleton<IConfiguration>(config);
        services.Configure<StorageOptions>(_ =>
        {
            _.Provider = storage.Provider;
            _.MaxImageBytes = storage.MaxImageBytes;
            _.Local = storage.Local;
            _.R2 = storage.R2;
        });
        services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(connectionString));
        services.AddSingleton<IObjectStorageService, CloudflareR2ObjectStorageService>();
        services.AddScoped<IProductImageBackfillStore, EfProductImageBackfillStore>();
        services.AddScoped<ProductImageR2BackfillRunner>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var options = new ProductImageBackfillOptions(
            EnvironmentName: environmentName,
            SourceRoot: Path.GetFullPath(sourceRoot),
            Execute: cli.Execute,
            ConfirmPhrase: cli.Confirm,
            BackfillFlagEnabled: backfillFlag.Enabled,
            StorageProvider: storage.Provider,
            R2Bucket: storage.R2.Bucket,
            R2PublicBaseUrl: storage.R2.PublicBaseUrl,
            KeyPrefix: string.IsNullOrWhiteSpace(storage.R2.KeyPrefix) ? "products" : storage.R2.KeyPrefix,
            ConnectionString: connectionString,
            ReportPath: cli.ReportPath);

        var runner = scope.ServiceProvider.GetRequiredService<ProductImageR2BackfillRunner>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("product-images-backfill-r2");

        try
        {
            var report = await runner.RunAsync(options);
            var markdown = ProductImageBackfillReportWriter.FormatMarkdown(report);

            var reportPath = options.ReportPath
                             ?? Path.Combine(
                                 "artifacts",
                                 "r2-backfill",
                                 $"report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            await File.WriteAllTextAsync(reportPath, markdown);

            Console.WriteLine(markdown);
            Console.WriteLine($"Report written to: {Path.GetFullPath(reportPath)}");
            logger.LogInformation(
                "Backfill finished mode={Mode} eligible={Eligible} uploaded={Uploaded} errors={Errors}",
                report.Mode,
                report.Eligible,
                report.Uploaded,
                report.Errors);

            return report.Errors > 0 && options.Execute ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ABORT: {ex.Message}");
            logger.LogError(ex, "Backfill aborted");
            return 1;
        }
    }

    private static BackfillCliArgs ParseBackfillArgs(string[] args)
    {
        string? environment = null;
        string? sourceRoot = null;
        string? confirm = null;
        string? reportPath = null;
        var execute = false;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--environment":
                    environment = RequireValue(args, ref i);
                    break;
                case "--source-root":
                    sourceRoot = RequireValue(args, ref i);
                    break;
                case "--confirm":
                    confirm = RequireValue(args, ref i);
                    break;
                case "--report":
                    reportPath = RequireValue(args, ref i);
                    break;
                case "--execute":
                    execute = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument: {args[i]}");
            }
        }

        if (execute && dryRun)
            throw new InvalidOperationException("Use either --dry-run or --execute, not both.");

        if (!execute && !dryRun)
            dryRun = true; // default dry-run

        return new BackfillCliArgs(environment, sourceRoot, execute && !dryRun, confirm, reportPath);
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new InvalidOperationException($"Missing value for {args[i]}");
        return args[++i];
    }

    private sealed record BackfillCliArgs(
        string? Environment,
        string? SourceRoot,
        bool Execute,
        string? Confirm,
        string? ReportPath);
}
