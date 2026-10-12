using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Entities;
using PharmaFlow.Api.Persistence.Seeding;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class CanonicalSeedTests
{
    private static JsonObject Source() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "demo-data", "catalog.json")))!.AsObject();

    private static CanonicalSeedData Read(JsonObject json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()));
        return CanonicalSeedData.Load(stream);
    }

    private static PharmaFlowDbContext Context(DbConnectionInterceptor? guard = null)
    {
        var options = new DbContextOptionsBuilder<PharmaFlowDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=seed_offline_tests;Timeout=1");
        if (guard is not null) options.AddInterceptors(guard);
        return new(options.Options);
    }

    [Fact]
    public void Embedded_source_matches_canonical_counts_and_all_parent_values()
    {
        var source = Source();
        var data = CanonicalSeedData.Load();
        Assert.Equal(9, data.Medications.Count);
        Assert.Equal(3, data.Branches.Count);
        Assert.Equal(21, data.Availability.Count);
        Assert.Equal(source["medications"]!.AsArray().Count, data.Medications.Count);
        Assert.Equal(source["branches"]!.AsArray().Count, data.Branches.Count);
        Assert.Equal(source["availability"]!.AsArray().Count, data.Availability.Count);
        for (var i = 0; i < data.Medications.Count; i++)
        {
            var expected = source["medications"]![i]!;
            var actual = data.Medications[i];
            Assert.Equal(expected["id"]!.GetValue<string>(), actual.Id);
            Assert.Equal(expected["slug"]!.GetValue<string>(), actual.Slug);
            Assert.Equal(expected["displayName"]!.GetValue<string>(), actual.DisplayName);
            Assert.Equal(expected["strengthLabel"]!.GetValue<string>(), actual.StrengthLabel);
            Assert.Equal(expected["formCode"]!.GetValue<string>(), actual.FormCode);
            Assert.Equal(expected["presentationLabel"]!.GetValue<string>(), actual.PresentationLabel);
            Assert.Equal(expected["categoryCode"]!.GetValue<string>(), actual.CategoryCode);
            Assert.Equal(expected["prescriptionRequired"]!.GetValue<bool>(), actual.PrescriptionRequired);
        }
        for (var i = 0; i < data.Branches.Count; i++)
        {
            var expected = source["branches"]![i]!;
            var actual = data.Branches[i];
            Assert.Equal(expected["id"]!.GetValue<string>(), actual.Id);
            Assert.Equal(expected["slug"]!.GetValue<string>(), actual.Slug);
            Assert.Equal(expected["name"]!.GetValue<string>(), actual.Name);
            Assert.Equal(expected["fictionalLocation"]!.GetValue<string>(), actual.FictionalLocation);
            Assert.Equal(expected["timeZone"]!.GetValue<string>(), actual.TimeZone);
        }
    }

    [Fact]
    public void Ingredient_positions_and_split_schedules_preserve_source_order_and_closed_days()
    {
        var source = Source();
        var data = Read(source);
        var ingredientCount = 0;
        foreach (var medication in source["medications"]!.AsArray())
        {
            var expected = medication!["activeIngredients"]!.AsArray();
            var actual = data.Ingredients.Where(row => row.MedicationId == medication["id"]!.GetValue<string>()).ToArray();
            Assert.Equal(expected.Count, actual.Length);
            for (var position = 0; position < expected.Count; position++)
            {
                Assert.Equal(position, actual[position].Position);
                Assert.Equal(expected[position]!.GetValue<string>(), actual[position].Ingredient);
            }
            ingredientCount += expected.Count;
        }
        Assert.Equal(ingredientCount, data.Ingredients.Count);
        var intervalCount = 0;
        var closedDays = 0;
        var splitDays = 0;
        string[] weekdays = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];
        foreach (var branch in source["branches"]!.AsArray())
        {
            for (var day = 0; day < weekdays.Length; day++)
            {
                var expected = branch!["openingHours"]![weekdays[day]]!.AsArray();
                var actual = data.OpeningIntervals.Where(row => row.PharmacyBranchId == branch["id"]!.GetValue<string>() && row.DayOfWeek == day + 1).ToArray();
                Assert.Equal(expected.Count, actual.Length);
                if (expected.Count == 0) { closedDays++; Assert.Empty(actual); }
                if (expected.Count > 1) splitDays++;
                for (var position = 0; position < expected.Count; position++)
                {
                    Assert.Equal(position, actual[position].Position);
                    Assert.Equal(TimeOnly.ParseExact(expected[position]!["opensAt"]!.GetValue<string>(), "HH:mm", CultureInfo.InvariantCulture), actual[position].OpensAt);
                    Assert.Equal(TimeOnly.ParseExact(expected[position]!["closesAt"]!.GetValue<string>(), "HH:mm", CultureInfo.InvariantCulture), actual[position].ClosesAt);
                }
                intervalCount += expected.Count;
            }
        }
        Assert.Equal(intervalCount, data.OpeningIntervals.Count);
        Assert.True(closedDays > 0);
        Assert.Equal(source["branches"]!.AsArray().Sum(branch => weekdays.Count(day => branch!["openingHours"]![day]!.AsArray().Count > 1)), splitDays);
    }

    [Fact]
    public void Split_interval_input_retains_both_positions_without_changing_the_canonical_file()
    {
        var source = Source();
        var branch = source["branches"]![0]!;
        var monday = branch["openingHours"]!["monday"]!.AsArray();
        var interval = monday[0]!;
        var opens = TimeOnly.ParseExact(interval["opensAt"]!.GetValue<string>(), "HH:mm", CultureInfo.InvariantCulture);
        var closes = TimeOnly.ParseExact(interval["closesAt"]!.GetValue<string>(), "HH:mm", CultureInfo.InvariantCulture);
        var midpoint = opens.AddMinutes((closes - opens).TotalMinutes / 2).ToString("HH:mm", CultureInfo.InvariantCulture);
        var second = interval.DeepClone();
        interval["closesAt"] = midpoint;
        second["opensAt"] = midpoint;
        monday.Add(second);
        var mapped = Read(source).OpeningIntervals.Where(row => row.PharmacyBranchId == branch["id"]!.GetValue<string>() && row.DayOfWeek == 1).ToArray();
        Assert.Equal(2, mapped.Length);
        Assert.Equal(new[] { 0, 1 }, mapped.Select(row => row.Position));
        Assert.Equal(mapped[0].ClosesAt, mapped[1].OpensAt);
        Assert.Equal(opens, mapped[0].OpensAt);
        Assert.Equal(closes, mapped[1].ClosesAt);
    }

    [Fact]
    public void Availability_preserves_exact_pairs_nullable_and_unavailable_prices_and_UTC_instants()
    {
        var source = Source()["availability"]!.AsArray();
        var data = CanonicalSeedData.Load();
        Assert.Equal(source.Count, data.Availability.Count);
        Assert.True(data.Availability.Count < data.Medications.Count * data.Branches.Count);
        Assert.Equal(data.Availability.Count, data.Availability.Select(row => (row.MedicationId, row.BranchId)).Distinct().Count());
        for (var i = 0; i < source.Count; i++)
        {
            var expected = source[i]!;
            var actual = data.Availability[i];
            Assert.Equal(expected["medicationId"]!.GetValue<string>(), actual.MedicationId);
            Assert.Equal(expected["branchId"]!.GetValue<string>(), actual.BranchId);
            Assert.Equal(expected["status"]!.GetValue<string>(), actual.Status);
            Assert.Equal(expected["referencePrice"]?["minorUnits"]?.GetValue<long>(), actual.PriceMinorUnits);
            Assert.Equal(expected["referencePrice"]?["currencyCode"]?.GetValue<string>(), actual.PriceCurrencyCode);
            Assert.Equal(TimeSpan.Zero, actual.UpdatedAt.Offset);
            Assert.Equal(expected["updatedAt"]!.GetValue<string>(), actual.UpdatedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        }
        Assert.Contains(data.Availability, row => row.PriceMinorUnits is null && row.PriceCurrencyCode is null);
        Assert.Contains(data.Availability, row => row.Status == "unavailable" && row.PriceMinorUnits is not null);
    }

    [Theory]
    [InlineData("duplicate-medication-id")]
    [InlineData("duplicate-medication-slug")]
    [InlineData("duplicate-branch-id")]
    [InlineData("duplicate-branch-slug")]
    [InlineData("duplicate-observation")]
    [InlineData("unknown-medication")]
    [InlineData("unknown-branch")]
    [InlineData("status")]
    [InlineData("negative-price")]
    [InlineData("unsafe-price")]
    [InlineData("missing-currency")]
    [InlineData("wrong-currency")]
    [InlineData("missing-price-amount")]
    [InlineData("timestamp")]
    [InlineData("opening-time")]
    [InlineData("reversed-interval")]
    [InlineData("overlap")]
    [InlineData("missing-day")]
    [InlineData("empty-ingredient")]
    [InlineData("empty-text")]
    public void Invalid_canonical_input_is_rejected_before_any_database_access(string fault)
    {
        var source = Source();
        var medication = source["medications"]![0]!;
        var branch = source["branches"]![0]!;
        var observation = source["availability"]![0]!;
        var interval = branch["openingHours"]!["monday"]![0]!;
        switch (fault)
        {
            case "duplicate-medication-id": source["medications"]![1]!["id"] = medication["id"]!.DeepClone(); break;
            case "duplicate-medication-slug": source["medications"]![1]!["slug"] = medication["slug"]!.DeepClone(); break;
            case "duplicate-branch-id": source["branches"]![1]!["id"] = branch["id"]!.DeepClone(); break;
            case "duplicate-branch-slug": source["branches"]![1]!["slug"] = branch["slug"]!.DeepClone(); break;
            case "duplicate-observation": source["availability"]!.AsArray().Add(observation.DeepClone()); break;
            case "unknown-medication": observation["medicationId"] = "missing"; break;
            case "unknown-branch": observation["branchId"] = "missing"; break;
            case "status": observation["status"] = "unknown"; break;
            case "negative-price": observation["referencePrice"]!["minorUnits"] = -1; break;
            case "unsafe-price": observation["referencePrice"]!["minorUnits"] = 9_007_199_254_740_992L; break;
            case "missing-currency": observation["referencePrice"]!.AsObject().Remove("currencyCode"); break;
            case "wrong-currency": observation["referencePrice"]!["currencyCode"] = "OTHER"; break;
            case "missing-price-amount": observation["referencePrice"]!.AsObject().Remove("minorUnits"); break;
            case "timestamp": observation["updatedAt"] = "2026-09-01T09:00:00+01:00"; break;
            case "opening-time": interval["opensAt"] = "25:00"; break;
            case "reversed-interval": interval["opensAt"] = "18:00"; break;
            case "overlap": branch["openingHours"]!["monday"]!.AsArray().Add(interval.DeepClone()); break;
            case "missing-day": branch["openingHours"]!.AsObject().Remove("sunday"); break;
            case "empty-ingredient": medication["activeIngredients"]![0] = ""; break;
            case "empty-text": medication["displayName"] = ""; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(source));
    }

    [Fact]
    public void Repeat_synchronization_has_no_additions_or_updates_and_corrects_known_values()
    {
        using var context = Context();
        var data = CanonicalSeedData.Load();
        CanonicalDatabaseSeeder.ApplyToTrackedEntities(context, data);
        Assert.Equal(data.Medications.Count + data.Ingredients.Count + data.Branches.Count + data.OpeningIntervals.Count + data.Availability.Count,
            context.ChangeTracker.Entries().Count(entry => entry.State == EntityState.Added));
        // Simulate the post-save tracking state without executing SQL or substituting the provider.
        context.ChangeTracker.AcceptAllChanges();
        CanonicalDatabaseSeeder.ApplyToTrackedEntities(context, CanonicalSeedData.Load());
        Assert.False(context.ChangeTracker.HasChanges());
        context.Medications.Local.First().DisplayName = "Changed demo value";
        context.Availability.Local.First().PriceMinorUnits = null;
        context.Availability.Local.First().PriceCurrencyCode = null;
        CanonicalDatabaseSeeder.ApplyToTrackedEntities(context, CanonicalSeedData.Load());
        Assert.Equal(data.Medications[0].DisplayName, context.Medications.Local.First().DisplayName);
        Assert.Equal(data.Availability[0].PriceMinorUnits, context.Availability.Local.First().PriceMinorUnits);
        Assert.Equal(data.Availability[0].PriceCurrencyCode, context.Availability.Local.First().PriceCurrencyCode);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Deleted);
    }

    [Fact]
    public void Noncanonical_parents_and_missing_observation_pairs_are_not_deleted_or_overwritten()
    {
        using var context = Context();
        var data = CanonicalSeedData.Load();
        CanonicalDatabaseSeeder.ApplyToTrackedEntities(context, data);
        var unrelated = new MedicationEntity { Id = "unrelated", Slug = "unrelated", DisplayName = "Other", StrengthLabel = "Other",
            FormCode = "Other", PresentationLabel = "Other", CategoryCode = "Other" };
        context.Add(unrelated);
        var missingPair = (from medication in data.Medications from branch in data.Branches
            where !data.Availability.Any(row => row.MedicationId == medication.Id && row.BranchId == branch.Id)
            select (medication.Id, BranchId: branch.Id)).First();
        var extra = new AvailabilityEntity { MedicationId = missingPair.Id, BranchId = missingPair.BranchId,
            Status = "unavailable", UpdatedAt = DateTimeOffset.Parse("2026-09-01T12:00:00Z", CultureInfo.InvariantCulture) };
        context.Add(extra);
        context.ChangeTracker.AcceptAllChanges();
        CanonicalDatabaseSeeder.ApplyToTrackedEntities(context, CanonicalSeedData.Load());
        Assert.False(context.ChangeTracker.HasChanges());
        Assert.Same(unrelated, context.Medications.Local.Single(row => row.Id == "unrelated"));
        Assert.Same(extra, context.Availability.Local.Single(row => row.MedicationId == missingPair.Id && row.BranchId == missingPair.BranchId));
    }

    [Fact]
    public void Slug_collision_fails_without_changing_any_tracked_rows()
    {
        using var context = Context();
        var data = CanonicalSeedData.Load();
        context.Attach(new MedicationEntity { Id = "unrelated", Slug = data.Medications[0].Slug, DisplayName = "Other", StrengthLabel = "Other",
            FormCode = "Other", PresentationLabel = "Other", CategoryCode = "Other" });
        Assert.Throws<InvalidOperationException>(() => CanonicalDatabaseSeeder.ApplyToTrackedEntities(context, data));
        Assert.False(context.ChangeTracker.HasChanges());
        Assert.Single(context.ChangeTracker.Entries());
    }

    [Fact]
    public void Extra_intervals_on_closed_canonical_days_fail_without_deleting_them()
    {
        using var context = Context();
        var data = CanonicalSeedData.Load();
        var closed = (from branch in data.Branches from day in Enumerable.Range(1, 7)
            where !data.OpeningIntervals.Any(row => row.PharmacyBranchId == branch.Id && row.DayOfWeek == day)
            select (branch.Id, Day: day)).First();
        context.Attach(new OpeningIntervalEntity { PharmacyBranchId = closed.Id, DayOfWeek = closed.Day, Position = 0,
            OpensAt = new(9, 0), ClosesAt = new(17, 0) });
        Assert.Throws<InvalidOperationException>(() => CanonicalDatabaseSeeder.ApplyToTrackedEntities(context, data));
        Assert.False(context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Transaction_connection_failure_cannot_stage_or_write_any_seed_rows()
    {
        var guard = new RejectConnection();
        using var context = Context(guard);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CanonicalDatabaseSeeder(context).SeedAsync());
        Assert.Equal(1, guard.Attempts);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Normal_API_startup_and_seeder_resolution_never_invoke_the_seed()
    {
        var guard = new RejectConnection();
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PharmaFlow"] = "Host=127.0.0.1;Port=1;Database=seed_offline_tests;Timeout=1",
                    ["Database:Password"] = null
                }));
            builder.ConfigureServices(services => services.AddDbContext<PharmaFlowDbContext>(options => options.AddInterceptors(guard)));
        });
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        Assert.Equal(21, (await ApiFactory.GetArrayAsync(client, "/api/availability")).Count);
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CanonicalDatabaseSeeder>());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>().ChangeTracker.Entries());
        Assert.Equal(0, guard.Attempts);
    }

    private sealed class RejectConnection : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            Attempts++;
            throw new InvalidOperationException("Seed tests must never open a PostgreSQL connection.");
        }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Seed tests must never open a PostgreSQL connection.");
        }
    }
}
