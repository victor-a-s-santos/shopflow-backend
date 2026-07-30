using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Shipping.Application.Exceptions;
using Vls.Shopflow.Shipping.Application.Options;
using Vls.Shopflow.Shipping.Application.Services;
using Vls.Shopflow.Shipping.Infrastructure.ViaCep;

namespace Vls.Shopflow.Shipping.UnitTests.Infrastructure;

public sealed class ViaCepPostalCodeLookupServiceTests
{
    [Theory]
    [InlineData("02310-000", "02310000")]
    [InlineData("02310000", "02310000")]
    [InlineData(" 02310 000 ", "02310000")]
    public void TryNormalize_AcceptsMaskedAndUnmasked(string input, string expected)
        => BrazilPostalCodeNormalizer.TryNormalize(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    [InlineData("02310-00")]
    public void TryNormalize_RejectsInvalid(string input)
        => BrazilPostalCodeNormalizer.TryNormalize(input).Should().BeNull();

    [Fact]
    public async Task Lookup_WhenFound_ReturnsNormalizedDtoWithoutRawPayload()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"cep":"01001-000","logradouro":"Praça da Sé","bairro":"Sé","localidade":"São Paulo","uf":"sp"}""",
                Encoding.UTF8,
                "application/json")
        });

        var sut = CreateSut(handler);
        var result = await sut.LookupBrazilPostalCodeAsync("01001000", CancellationToken.None);

        result.Found.Should().BeTrue();
        result.PostalCode.Should().Be("01001-000");
        result.Street.Should().Be("Praça da Sé");
        result.Neighborhood.Should().Be("Sé");
        result.City.Should().Be("São Paulo");
        result.State.Should().Be("SP");
        result.Country.Should().Be("BR");
        result.Source.Should().Be("ViaCep");
        handler.LastRequestUri!.ToString().Should().Contain("/ws/01001000/json/");
    }

    [Fact]
    public async Task Lookup_WhenProviderNotFound_ReturnsFoundFalse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"erro":true}""", Encoding.UTF8, "application/json")
        });

        var sut = CreateSut(handler);
        var result = await sut.LookupBrazilPostalCodeAsync("00000000", CancellationToken.None);

        result.Found.Should().BeFalse();
        result.PostalCode.Should().Be("00000-000");
        result.Street.Should().BeNull();
        result.Source.Should().Be("ViaCep");
    }

    [Fact]
    public async Task Lookup_WhenProviderHttpFails_ThrowsUnavailable()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var sut = CreateSut(handler);

        var act = () => sut.LookupBrazilPostalCodeAsync("01001000", CancellationToken.None);
        await act.Should().ThrowAsync<PostalCodeLookupUnavailableException>();
    }

    [Fact]
    public async Task Lookup_WhenDisabled_ThrowsUnavailable()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not call"));
        var sut = CreateSut(handler, enabled: false);

        var act = () => sut.LookupBrazilPostalCodeAsync("01001000", CancellationToken.None);
        await act.Should().ThrowAsync<PostalCodeLookupUnavailableException>();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Lookup_WhenInvalidCepPassedToService_ThrowsArgument()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not call"));
        var sut = CreateSut(handler);

        var act = () => sut.LookupBrazilPostalCodeAsync("123", CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
        handler.CallCount.Should().Be(0);
    }

    private static ViaCepPostalCodeLookupService CreateSut(StubHandler handler, bool enabled = true)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://viacep.com.br/")
        };

        return new ViaCepPostalCodeLookupService(
            client,
            Options.Create(new PostalCodeLookupOptions
            {
                Enabled = enabled,
                Provider = "ViaCep",
                BaseUrl = "https://viacep.com.br",
                TimeoutSeconds = 5
            }),
            NullLogger<ViaCepPostalCodeLookupService>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(responder(request));
        }
    }
}
