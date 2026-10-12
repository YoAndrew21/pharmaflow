using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;
using PharmaFlow.Api.Services;

var seedRequested = args.Contains("--seed", StringComparer.Ordinal);
// Consume the bare seed switch before the configuration command-line provider parses it.
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--seed").ToArray());

// Add services to the container.

builder.Services.AddControllers();
// Registration is lazy; current services remain in memory and do not resolve or connect this context.
builder.Services.AddDbContext<PharmaFlowDbContext>(options =>
{
    var connectionString = new NpgsqlConnectionStringBuilder(
        builder.Configuration.GetConnectionString("PharmaFlow"))
    {
        Password = builder.Configuration["Database:Password"]
    };
    options.UseNpgsql(connectionString.ConnectionString);
});
builder.Services.AddSingleton<IMedicationService, InMemoryMedicationService>();
builder.Services.AddSingleton<IPharmacyService, InMemoryPharmacyService>();
builder.Services.AddSingleton<IAvailabilityService, InMemoryAvailabilityService>();
builder.Services.AddScoped<CanonicalDatabaseSeeder>();
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

// Fail startup clearly if canonical observations reference missing medication or branch identities.
_ = app.Services.GetRequiredService<IAvailabilityService>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Expose the entry point to WebApplicationFactory without changing application behavior.
public partial class Program { }
