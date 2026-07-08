namespace Vls.Shopflow.HttpApi.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", (IHostEnvironment environment) =>
            Results.Json(new
            {
                status = "ok",
                environment = environment.EnvironmentName
            }))
            .AllowAnonymous()
            .WithTags("Health")
            .WithName("GetHealth");
    }
}
