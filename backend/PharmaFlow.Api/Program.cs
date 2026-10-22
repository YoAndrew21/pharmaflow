using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Npgsql;
using PharmaFlow.Api.Hosting;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;
using PharmaFlow.Api.Services;

var seedRequested = args.Contains("--seed", StringComparer.Ordinal);
// Consume the bare seed switch before the configuration command-line provider parses it.
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--seed").ToArray());

// A platform PORT overrides the image's HTTP default only in Production.
if (builder.Environment.IsProduction() && builder.Configuration["PORT"] is { } portValue)
{
    if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    if (!builder.Configuration.GetValue<bool>("ReverseProxy:Enabled")) return;
    var proxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    if (proxies.Length == 0 || proxies.Any(value => !IPAddress.TryParse(value, out _)))
        throw new InvalidOperationException("ReverseProxy requires explicit valid KnownProxies IP addresses.");
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var proxy in proxies) options.KnownProxies.Add(IPAddress.Parse(proxy));
});
builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap["frontend"] = typeof(FrontendRouteConstraint));

// Add services to the container.

builder.Services.AddControllers();
// Registration and reader construction are lazy; startup never opens a database connection.
builder.Services.AddDbContext<PharmaFlowDbContext>(options =>
{
    var connectionString = new NpgsqlConnectionStringBuilder(
        builder.Configuration.GetConnectionString("PharmaFlow"))
    {
        Password = builder.Configuration["Database:Password"]
    };
    options.UseNpgsql(connectionString.ConnectionString);
});
builder.Services.AddSingleton<CanonicalCatalogOrder>();
builder.Services.AddScoped<IMedicationService, DatabaseMedicationService>();
builder.Services.AddScoped<IPharmacyService, DatabasePharmacyService>();
builder.Services.AddScoped<IAvailabilityService, DatabaseAvailabilityService>();
builder.Services.AddScoped<CanonicalDatabaseSeeder>();
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database");
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if (seedRequested)
{
    await using (app)
    {
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Canonical demo seeding is available only in Development.");
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CanonicalDatabaseSeeder>().SeedAsync();
        Console.WriteLine("Canonical demo seed completed.");
    }
    return;
}

// Configure the HTTP request pipeline.
if (app.Configuration.GetValue<bool>("ReverseProxy:Enabled")) app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(handler => handler.Run(context =>
        Results.Problem(statusCode: 500, title: "An unexpected error occurred.").ExecuteAsync(context)));
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            return Task.CompletedTask;
        });
        await next(context);
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Configuration.GetValue<bool>("Http:DisableHttpsRedirection"))
    app.UseHttpsRedirection();

// Development's API-only image has no webroot. The Production image supplies it.
var hasFrontend = File.Exists(Path.Combine(app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html"));
if (hasFrontend) app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/ready");
if (hasFrontend) app.MapFallbackToFile("{*path:nonfile:frontend}", "index.html");

app.Run();

// Expose the entry point to WebApplicationFactory without changing application behavior.
public partial class Program { }
