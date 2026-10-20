using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaFlow.Api.Tests.Infrastructure;
using Xunit;

namespace PharmaFlow.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ReadinessTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(false, HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task HTTP_redirection_is_preserved_unless_explicitly_disabled(bool disable, HttpStatusCode expected)
    {
        await using var original = new ApiFactory();
        await using var factory = original.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Http:DisableHttpsRedirection"] = disable.ToString() }));
            builder.ConfigureServices(services => services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 7043));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/api/medications");
        Assert.Equal(expected, response.StatusCode);
        if (!disable) Assert.Equal("https", response.Headers.Location?.Scheme);
    }

    [Fact]
    public async Task Readiness_succeeds_with_the_isolated_PostgreSQL_connection()
    {
        using var client = fixture.Factory.CreateApiClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_returns_only_a_generic_failure_when_database_is_unreachable()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }
}
