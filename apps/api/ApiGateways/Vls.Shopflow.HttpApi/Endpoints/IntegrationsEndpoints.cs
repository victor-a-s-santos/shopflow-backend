using Vls.Shopflow.IdentityAccess.Infrastructure;
using Vls.Shopflow.Shipping.Application.Exceptions;
using Vls.Shopflow.Shipping.Application.Interfaces;
using Vls.Shopflow.Shipping.Application.Services;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class IntegrationsEndpoints
{
    public static RouteGroupBuilder MapIntegrationsEndpoints(this RouteGroupBuilder group)
    {
        var integrations = group.MapGroup("/integrations").WithTags("Integrations");

        integrations.MapGet("/postal-code/br/{cep}", async (
            string cep,
            IPostalCodeLookupService lookup,
            CancellationToken ct) =>
        {
            var digits = BrazilPostalCodeNormalizer.TryNormalize(cep);
            if (digits is null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["cep"] = ["Informe um CEP válido com 8 dígitos."]
                    },
                    title: "Validation failed",
                    detail: "CEP inválido.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var result = await lookup.LookupBrazilPostalCodeAsync(digits, ct);
                return Results.Ok(result);
            }
            catch (PostalCodeLookupUnavailableException ex)
            {
                return Results.Problem(
                    title: "Postal code lookup unavailable",
                    detail: "Não foi possível consultar o CEP no momento. Preencha o endereço manualmente.",
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = PostalCodeLookupUnavailableException.ErrorCode,
                        ["message"] = ex.Message
                    });
            }
        })
        .RequireRateLimiting(DependencyInjection.PostalCodeLookupRateLimitPolicy)
        .AllowAnonymous();

        return group;
    }
}
