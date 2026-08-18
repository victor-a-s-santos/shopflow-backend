using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Shipping.Application.DataTransferObjects;
using Vls.Shopflow.Shipping.Application.Exceptions;
using Vls.Shopflow.Shipping.Application.Interfaces;
using Vls.Shopflow.Shipping.Application.Options;
using Vls.Shopflow.Shipping.Application.Services;

namespace Vls.Shopflow.Shipping.Infrastructure.ViaCep;

public sealed class ViaCepPostalCodeLookupService(
    HttpClient httpClient,
    IOptions<PostalCodeLookupOptions> options,
    ILogger<ViaCepPostalCodeLookupService> logger)
    : IPostalCodeLookupService
{
    public const string ProviderSourceName = "ViaCep";

    public async Task<BrazilPostalCodeLookupDto> LookupBrazilPostalCodeAsync(
        string cep,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
            throw new PostalCodeLookupUnavailableException("Postal code lookup is disabled.");

        var digits = BrazilPostalCodeNormalizer.TryNormalize(cep)
                     ?? throw new ArgumentException("CEP must be exactly 8 digits.", nameof(cep));

        var masked = BrazilPostalCodeNormalizer.FormatMasked(digits);

        try
        {
            using var response = await httpClient.GetAsync($"ws/{digits}/json/", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "ViaCEP returned HTTP {StatusCode} for postal lookup.",
                    (int)response.StatusCode);
                throw new PostalCodeLookupUnavailableException(
                    $"Postal code provider returned HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<ViaCepJsonResponse>(
                cancellationToken: cancellationToken);

            if (payload is null)
            {
                logger.LogWarning("ViaCEP returned an empty body for postal lookup.");
                throw new PostalCodeLookupUnavailableException("Postal code provider returned an empty body.");
            }

            if (IsErroFlag(payload.Erro))
            {
                return new BrazilPostalCodeLookupDto(
                    PostalCode: masked,
                    Found: false,
                    Country: "BR",
                    Source: ProviderSourceName);
            }

            return new BrazilPostalCodeLookupDto(
                PostalCode: masked,
                Found: true,
                Street: NullIfWhiteSpace(payload.Logradouro),
                Neighborhood: NullIfWhiteSpace(payload.Bairro),
                City: NullIfWhiteSpace(payload.Localidade),
                State: NullIfWhiteSpace(payload.Uf)?.ToUpperInvariant(),
                Country: "BR",
                Source: ProviderSourceName);
        }
        catch (PostalCodeLookupUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("ViaCEP postal lookup timed out.");
            throw new PostalCodeLookupUnavailableException("Postal code provider timed out.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "ViaCEP postal lookup failed.");
            throw new PostalCodeLookupUnavailableException("Postal code provider is unavailable.", ex);
        }
    }

    private static bool IsErroFlag(JsonElement erro)
    {
        return erro.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(erro.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ViaCepJsonResponse
    {
        [JsonPropertyName("erro")]
        public JsonElement Erro { get; set; }

        [JsonPropertyName("cep")]
        public string? Cep { get; set; }

        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("localidade")]
        public string? Localidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }
    }
}
