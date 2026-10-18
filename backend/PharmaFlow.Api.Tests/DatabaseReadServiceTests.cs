using System.Data.Common;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;
using PharmaFlow.Api.Services;
using PharmaFlow.Api.Tests.Infrastructure;
using Xunit;

namespace PharmaFlow.Api.Tests;

// Separate container from the unchanged canonical contract collection. Mutations are test-only.
public sealed class DatabaseReadServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ClearApplicationDataAsync();
        await fixture.SeedCanonicalAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("medications")]
    [InlineData("medication-detail")]
    [InlineData("pharmacies")]
    [InlineData("pharmacy-detail")]
    [InlineData("availability")]
    public async Task Each_operation_uses_one_async_SELECT_propagates_token_and_tracks_nothing(string operation)
    {
        var probe = new ReadProbe();
        using var factory = fixture.CreateFactory(probe);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
        using var cancellation = new CancellationTokenSource();
        await Read(scope.ServiceProvider, operation, cancellation.Token);
        Assert.Equal(1, probe.Queries);
        Assert.Equal(cancellation.Token, probe.Token);
        Assert.Empty(context.ChangeTracker.Entries());
        // Both detail queries explicitly enforce PostgreSQL C-collation equality.
        if (operation.EndsWith("-detail", StringComparison.Ordinal)) Assert.Contains("COLLATE \"C\"", probe.LastSql);
    }

    [Theory]
    [InlineData("medications")]
    [InlineData("medication-detail")]
    [InlineData("pharmacies")]
    [InlineData("pharmacy-detail")]
    [InlineData("availability")]
    public async Task Canceled_read_does_not_return_fixture_data(string operation)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Read(scope.ServiceProvider, operation, cancellation.Token));
    }

    [Fact]
    public void Production_startup_and_reader_resolution_do_not_query_or_seed_the_database()
    {
        var probe = new ReadProbe { Fail = true };
        using var factory = fixture.CreateFactory(probe);
        using var scope = factory.Services.CreateScope();
        Assert.IsType<DatabaseMedicationService>(scope.ServiceProvider.GetRequiredService<IMedicationService>());
        Assert.IsType<DatabasePharmacyService>(scope.ServiceProvider.GetRequiredService<IPharmacyService>());
        Assert.IsType<DatabaseAvailabilityService>(scope.ServiceProvider.GetRequiredService<IAvailabilityService>());
        Assert.Equal(0, probe.Queries);
    }

    [Theory]
    [InlineData("/api/medications/PARACETAMOL-500-TABLETS")]
    [InlineData("/api/pharmacies/ASTER-COMMONS")]
    [InlineData("/api/medications/%20paracetamol-500-tablets")]
    [InlineData("/api/pharmacies/aster-commons%20")]
    public async Task Case_altered_or_nonexact_slugs_remain_404_problem_details(string path)
    {
        using var client = fixture.Factory.CreateApiClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Arbitrary_availability_query_strings_do_not_filter()
    {
        using var client = fixture.Factory.CreateApiClient();
        var all = await ApiFactory.GetArrayAsync(client, "/api/availability");
        var queried = await ApiFactory.GetArrayAsync(client,
            "/api/availability?medicationId=missing&branchId=missing&status=unavailable&unexpected=value");
        Assert.True(JsonNode.DeepEquals(all, queried));
    }

    [Fact]
    public async Task Database_edits_are_visible_through_all_five_routes_including_nested_values()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
            fixture.AssertContainerTarget(context);
            var medication = await context.Medications.SingleAsync(row => row.Id == "med-par-500");
            medication.DisplayName = "Database-only display";
            medication.StrengthLabel = "Database-only strength";
            medication.FormCode = "database-only-form";
            medication.PresentationLabel = "Database-only package";
            medication.CategoryCode = "database-only-category";
            medication.PrescriptionRequired = !medication.PrescriptionRequired;
            var ingredients = await context.MedicationActiveIngredients.Where(row => row.MedicationId == "med-par-caf").OrderBy(row => row.Position).ToListAsync();
            ingredients[0].Ingredient = "Database first";
            ingredients[1].Ingredient = "Database second";
            var branch = await context.PharmacyBranches.SingleAsync(row => row.Id == "aster-commons");
            branch.Name = "Database-only branch";
            branch.FictionalLocation = "Database-only fictional location";
            branch.TimeZone = "Etc/GMT";
            var monday = await context.OpeningIntervals.SingleAsync(row => row.PharmacyBranchId == branch.Id && row.DayOfWeek == 1);
            monday.ClosesAt = new(12, 0);
            context.OpeningIntervals.Add(new() { PharmacyBranchId = branch.Id, DayOfWeek = 1, Position = 1, OpensAt = new(14, 0), ClosesAt = new(18, 0) });
            var observation = await context.Availability.SingleAsync(row => row.MedicationId == medication.Id && row.BranchId == branch.Id);
            observation.Status = "low-stock";
            observation.PriceMinorUnits = 12345;
            observation.PriceCurrencyCode = "USD";
            observation.UpdatedAt = new DateTimeOffset(2026, 10, 1, 1, 2, 3, TimeSpan.Zero);
            await context.SaveChangesAsync();
        }
        using var client = fixture.Factory.CreateApiClient();
        var medications = await ApiFactory.GetArrayAsync(client, "/api/medications");
        var detail = JsonNode.Parse(await client.GetStringAsync("/api/medications/paracetamol-500-tablets"))!;
        Assert.True(JsonNode.DeepEquals(detail, medications.Single(row => row!["id"]!.GetValue<string>() == "med-par-500")));
        Assert.Equal("Database-only display", detail["displayName"]!.GetValue<string>());
        Assert.Equal("Database-only strength", detail["strengthLabel"]!.GetValue<string>());
        Assert.Equal("database-only-form", detail["formCode"]!.GetValue<string>());
        Assert.Equal("Database-only package", detail["presentationLabel"]!.GetValue<string>());
        Assert.Equal("database-only-category", detail["categoryCode"]!.GetValue<string>());
        Assert.True(detail["prescriptionRequired"]!.GetValue<bool>());
        var combination = medications.Single(row => row!["id"]!.GetValue<string>() == "med-par-caf")!;
        Assert.Equal(new[] { "Database first", "Database second" }, combination["activeIngredients"]!.AsArray().Select(row => row!.GetValue<string>()));
        var branches = await ApiFactory.GetArrayAsync(client, "/api/pharmacies");
        var branchDetail = JsonNode.Parse(await client.GetStringAsync("/api/pharmacies/aster-commons"))!;
        Assert.True(JsonNode.DeepEquals(branchDetail, branches.Single(row => row!["id"]!.GetValue<string>() == "aster-commons")));
        Assert.Equal("Database-only branch", branchDetail["name"]!.GetValue<string>());
        Assert.Equal("Database-only fictional location", branchDetail["fictionalLocation"]!.GetValue<string>());
        Assert.Equal("Etc/GMT", branchDetail["timeZone"]!.GetValue<string>());
        var hours = branchDetail["openingHours"]!.AsObject();
        Assert.Equal(new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" }, hours.Select(row => row.Key));
        Assert.Empty(hours["sunday"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("[{\"opensAt\":\"09:00\",\"closesAt\":\"12:00\"},{\"opensAt\":\"14:00\",\"closesAt\":\"18:00\"}]"), hours["monday"]));
        var availability = await ApiFactory.GetArrayAsync(client, "/api/availability");
        var updated = availability.Single(row => row!["medicationId"]!.GetValue<string>() == "med-par-500" && row["branchId"]!.GetValue<string>() == "aster-commons")!;
        Assert.Equal("low-stock", updated["status"]!.GetValue<string>());
        Assert.Equal(12345, updated["referencePrice"]!["minorUnits"]!.GetValue<long>());
        Assert.Equal("USD", updated["referencePrice"]!["currencyCode"]!.GetValue<string>());
        Assert.Equal("2026-10-01T01:02:03Z", updated["updatedAt"]!.GetValue<string>());
    }

    [Fact]
    public async Task Empty_database_returns_empty_collections_and_404_details_without_fallback()
    {
        await fixture.ClearApplicationDataAsync();
        using var client = fixture.Factory.CreateApiClient();
        foreach (var path in new[] { "/api/medications", "/api/pharmacies", "/api/availability" })
            Assert.Empty(await ApiFactory.GetArrayAsync(client, path));
        foreach (var path in new[] { "/api/medications/paracetamol-500-tablets", "/api/pharmacies/aster-commons" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Removing_an_observation_does_not_recreate_it_or_call_it_unavailable()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
            fixture.AssertContainerTarget(context);
            await context.Availability.Where(row => row.MedicationId == "med-par-500" && row.BranchId == "aster-commons").ExecuteDeleteAsync();
        }
        using var client = fixture.Factory.CreateApiClient();
        var rows = await ApiFactory.GetArrayAsync(client, "/api/availability");
        Assert.Equal(20, rows.Count);
        Assert.DoesNotContain(rows, row => row!["medicationId"]!.GetValue<string>() == "med-par-500" && row["branchId"]!.GetValue<string>() == "aster-commons");
    }

    [Theory]
    [InlineData("/api/medications")]
    [InlineData("/api/medications/paracetamol-500-tablets")]
    [InlineData("/api/pharmacies")]
    [InlineData("/api/pharmacies/aster-commons")]
    [InlineData("/api/availability")]
    public async Task Database_query_failures_remain_failures_instead_of_successful_fixture_responses(string path)
    {
        var probe = new ReadProbe { Fail = true };
        using var factory = fixture.CreateFactory(probe);
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, probe.Queries);
    }

    [Fact]
    public async Task Reversed_physical_insertion_order_does_not_change_any_canonical_collection()
    {
        await fixture.ClearApplicationDataAsync();
        var data = CanonicalSeedData.Load();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
            fixture.AssertContainerTarget(context);
            async Task InsertInReverse<T>(IEnumerable<T> rows) where T : class
            {
                foreach (var row in rows.Reverse()) { context.Add(row); await context.SaveChangesAsync(); }
            }
            await InsertInReverse(data.Medications);
            await InsertInReverse(data.Branches);
            await InsertInReverse(data.Ingredients);
            await InsertInReverse(data.OpeningIntervals);
            await InsertInReverse(data.Availability);
        }
        using var client = fixture.Factory.CreateApiClient();
        foreach (var (resource, source) in new[] { ("medications", "medications"), ("pharmacies", "branches"), ("availability", "availability") })
            Assert.True(JsonNode.DeepEquals(fixture.Factory.Canonical[source], await ApiFactory.GetArrayAsync(client, "/api/" + resource)));
    }

    [Fact]
    public async Task Unknown_database_identities_follow_canonical_records_in_ordinal_order()
    {
        var data = CanonicalSeedData.Load();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
            fixture.AssertContainerTarget(context);
            foreach (var suffix in new[] { "z", "a" })
            {
                var medication = data.Medications[0];
                var branch = data.Branches[0];
                context.Medications.Add(new() { Id = "test-" + suffix, Slug = "test-" + suffix, DisplayName = medication.DisplayName,
                    StrengthLabel = medication.StrengthLabel, FormCode = medication.FormCode, PresentationLabel = medication.PresentationLabel,
                    CategoryCode = medication.CategoryCode, PrescriptionRequired = medication.PrescriptionRequired });
                context.PharmacyBranches.Add(new() { Id = "test-" + suffix, Slug = "test-" + suffix, Name = branch.Name,
                    FictionalLocation = branch.FictionalLocation, TimeZone = branch.TimeZone });
                context.Availability.Add(new() { MedicationId = "test-" + suffix, BranchId = branch.Id, Status = "available",
                    UpdatedAt = data.Availability[0].UpdatedAt });
            }
            await context.SaveChangesAsync();
        }
        using var client = fixture.Factory.CreateApiClient();
        foreach (var path in new[] { "/api/medications", "/api/pharmacies" })
        {
            var rows = await ApiFactory.GetArrayAsync(client, path);
            Assert.Equal(new[] { "test-a", "test-z" }, rows.TakeLast(2).Select(row => row!["id"]!.GetValue<string>()));
        }
        var availability = await ApiFactory.GetArrayAsync(client, "/api/availability");
        Assert.Equal(new[] { "test-a", "test-z" }, availability.TakeLast(2).Select(row => row!["medicationId"]!.GetValue<string>()));
    }

    private static async Task Read(IServiceProvider services, string operation, CancellationToken token)
    {
        switch (operation)
        {
            case "medications": await services.GetRequiredService<IMedicationService>().GetAllAsync(token); break;
            case "medication-detail": await services.GetRequiredService<IMedicationService>().GetBySlugAsync("paracetamol-500-tablets", token); break;
            case "pharmacies": await services.GetRequiredService<IPharmacyService>().GetAllAsync(token); break;
            case "pharmacy-detail": await services.GetRequiredService<IPharmacyService>().GetBySlugAsync("aster-commons", token); break;
            case "availability": await services.GetRequiredService<IAvailabilityService>().GetAllAsync(token); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private sealed class ReadProbe : DbCommandInterceptor
    {
        public int Queries { get; private set; }
        public bool Fail { get; init; }
        public CancellationToken Token { get; private set; }
        public string LastSql { get; private set; } = "";
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            => throw new InvalidOperationException("Database readers must execute asynchronously.");
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Queries++;
            Token = cancellationToken;
            LastSql = command.CommandText;
            Assert.StartsWith("SELECT", LastSql.TrimStart());
            if (Fail) throw new InvalidOperationException("Isolated query failure for fallback regression testing.");
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Read services must never execute database writes.");
    }
}
