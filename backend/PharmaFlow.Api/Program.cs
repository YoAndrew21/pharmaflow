using PharmaFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<IMedicationService, InMemoryMedicationService>();
builder.Services.AddSingleton<IPharmacyService, InMemoryPharmacyService>();
builder.Services.AddSingleton<IAvailabilityService, InMemoryAvailabilityService>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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
