using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class ProductionHostingTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/catalog")]
    [InlineData("/medications/paracetamol-500-tablets")]
    [InlineData("/pharmacies/aster-commons")]
    [InlineData("/favorites")]
    [InlineData("/unknown-frontend-route")]
    public async Task Frontend_routes_serve_the_entry_document(string path)
    {
        await using var factory = new ProductionFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("test frontend entry", await response.Content.ReadAsStringAsync());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/unknown")]
    [InlineData("/API/unknown")]
    [InlineData("/health")]
    [InlineData("/health/unknown")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/openapi")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/assets/missing")]
    [InlineData("/assets/missing.js")]
    public async Task Reserved_routes_never_fall_back_to_HTML(string path)
    {
        await using var factory = new ProductionFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("test frontend entry", await response.Content.ReadAsStringAsync());
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Compiled_asset_path_is_served_and_API_method_semantics_are_preserved()
    {
        await using var factory = new ProductionFactory();
        using var client = factory.Client();
        using var asset = await client.GetAsync("/assets/test.js");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("test asset", await asset.Content.ReadAsStringAsync());
        using var write = await client.PostAsync("/api/medications", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
    }

    [Fact]
    public async Task Database_failure_has_generic_Production_error_and_readiness_without_HTML()
    {
        await using var factory = new ProductionFactory();
        using var client = factory.Client();
        using var failed = await client.GetAsync("/api/medications");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);
        var problem = await failed.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("An unexpected error occurred.", problem?.Title);
        Assert.Null(problem?.Detail);
        Assert.Null(problem?.Instance);
        var body = await failed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("pharmaflow_offline_production_tests", body);
        Assert.DoesNotContain("test-only-not-a-secret", body);
        Assert.Equal("nosniff", failed.Headers.GetValues("X-Content-Type-Options").Single());
        using var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Equal("Unhealthy", await readiness.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false, "203.0.113.10", HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, "203.0.113.11", HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, "203.0.113.10", HttpStatusCode.OK)]
    public async Task Forwarded_HTTPS_is_accepted_only_when_enabled_and_from_a_trusted_proxy(
        bool enabled, string remoteAddress, HttpStatusCode expected)
    {
        await using var factory = new ProductionFactory(enabled, remoteAddress);
        using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "198.51.100.2");
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.TemporaryRedirect)
            Assert.Equal("https", response.Headers.Location?.Scheme);
    }

    private sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        private readonly string webroot = Path.Combine(Path.GetTempPath(), "pharmaflow-production-" + Guid.NewGuid().ToString("N"));
        private readonly bool? proxyEnabled;
        private readonly string remoteAddress;

        public ProductionFactory(bool? proxyEnabled = null, string remoteAddress = "203.0.113.10")
        {
            this.proxyEnabled = proxyEnabled;
            this.remoteAddress = remoteAddress;
            Directory.CreateDirectory(Path.Combine(webroot, "assets"));
            File.WriteAllText(Path.Combine(webroot, "index.html"), "<!doctype html><title>test frontend entry</title>");
            File.WriteAllText(Path.Combine(webroot, "assets", "test.js"), "// test asset");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseWebRoot(webroot);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PharmaFlow"] = "Host=127.0.0.1;Port=1;Database=pharmaflow_offline_production_tests;Timeout=1",
                    ["Database:Password"] = "test-only-not-a-secret",
                    ["Http:DisableHttpsRedirection"] = (proxyEnabled is null).ToString(),
                    ["ReverseProxy:Enabled"] = (proxyEnabled ?? false).ToString(),
                    ["ReverseProxy:KnownProxies:0"] = "203.0.113.10"
                });
            });
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 7043);
                services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(remoteAddress));
            });
        }

        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false
        });

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            Directory.Delete(webroot, recursive: true);
        }
    }

    private sealed class RemoteAddressFilter(string address) : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
            Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(address);
                return following(context);
            });
            next(app);
        };
    }
}
