using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Entities;
using Xunit;

namespace PharmaFlow.Api.Tests;

// Provider-specific metadata only: no PostgreSQL connection and no EF InMemory substitute.
public sealed class PersistenceModelTests
{
    private static PharmaFlowDbContext CreateContext() => new(
        new DbContextOptionsBuilder<PharmaFlowDbContext>().UseNpgsql().Options);

    private static IModel Model(PharmaFlowDbContext context) => context.GetService<IDesignTimeModel>().Model;

    [Theory]
    [InlineData(typeof(MedicationEntity), "medications")]
    [InlineData(typeof(MedicationActiveIngredientEntity), "medication_active_ingredients")]
    [InlineData(typeof(PharmacyBranchEntity), "pharmacy_branches")]
    [InlineData(typeof(OpeningIntervalEntity), "opening_intervals")]
    [InlineData(typeof(AvailabilityEntity), "availability")]
    public void Persistence_types_map_to_the_five_approved_tables(Type type, string table)
    {
        using var context = CreateContext();
        var model = Model(context);
        Assert.Equal(5, model.GetEntityTypes().Count());
        Assert.Equal(table, model.FindEntityType(type)!.GetTableName());
        Assert.All(model.GetEntityTypes(), entity => Assert.Equal("PharmaFlow.Api.Persistence.Entities", entity.ClrType.Namespace));
    }

    [Theory]
    [InlineData(typeof(MedicationEntity), new[] { "Id" })]
    [InlineData(typeof(PharmacyBranchEntity), new[] { "Id" })]
    [InlineData(typeof(MedicationActiveIngredientEntity), new[] { "MedicationId", "Position" })]
    [InlineData(typeof(OpeningIntervalEntity), new[] { "PharmacyBranchId", "DayOfWeek", "Position" })]
    [InlineData(typeof(AvailabilityEntity), new[] { "MedicationId", "BranchId" })]
    public void Primary_keys_preserve_identity_and_ordered_child_positions(Type type, string[] properties)
    {
        using var context = CreateContext();
        var key = Model(context).FindEntityType(type)!.FindPrimaryKey()!;
        Assert.Equal(properties, key.Properties.Select(property => property.Name));
        Assert.All(key.Properties, property => Assert.Equal(ValueGenerated.Never, property.ValueGenerated));
    }

    [Fact]
    public void Only_price_columns_are_nullable_and_strings_are_required_text()
    {
        using var context = CreateContext();
        foreach (var entity in Model(context).GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var nullablePrice = entity.ClrType == typeof(AvailabilityEntity) &&
                    property.Name is "PriceMinorUnits" or "PriceCurrencyCode";
                Assert.Equal(nullablePrice, property.IsNullable);
                Assert.False(property.IsShadowProperty());
                if (property.ClrType == typeof(string)) Assert.Equal("text", property.GetColumnType());
            }
        }
    }

    [Fact]
    public void Medication_and_branch_slugs_have_unique_indexes()
    {
        using var context = CreateContext();
        foreach (var type in new[] { typeof(MedicationEntity), typeof(PharmacyBranchEntity) })
        {
            var index = Assert.Single(Model(context).FindEntityType(type)!.GetIndexes());
            Assert.True(index.IsUnique);
            Assert.Equal("Slug", Assert.Single(index.Properties).Name);
        }
    }

    [Theory]
    [InlineData(typeof(MedicationActiveIngredientEntity), typeof(MedicationEntity), "MedicationId", DeleteBehavior.Cascade)]
    [InlineData(typeof(OpeningIntervalEntity), typeof(PharmacyBranchEntity), "PharmacyBranchId", DeleteBehavior.Cascade)]
    [InlineData(typeof(AvailabilityEntity), typeof(MedicationEntity), "MedicationId", DeleteBehavior.Restrict)]
    [InlineData(typeof(AvailabilityEntity), typeof(PharmacyBranchEntity), "BranchId", DeleteBehavior.Restrict)]
    public void Foreign_keys_preserve_required_parent_relationships(Type dependent, Type principal, string property, DeleteBehavior deleteBehavior)
    {
        using var context = CreateContext();
        var foreignKey = Model(context).FindEntityType(dependent)!.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == principal);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal("Id", Assert.Single(foreignKey.PrincipalKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.NotNull(foreignKey.DependentToPrincipal);
        Assert.NotNull(foreignKey.PrincipalToDependent);
    }

    [Fact]
    public void Check_constraints_define_canonical_status_and_nullable_price_semantics()
    {
        using var context = CreateContext();
        var checks = Model(context).FindEntityType(typeof(AvailabilityEntity))!.GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);
        Assert.Equal(3, checks.Count);
        Assert.Equal("status IN ('available', 'low-stock', 'unavailable')", checks["ck_availability_status"]);
        Assert.Equal("price_minor_units IS NULL OR price_minor_units >= 0", checks["ck_availability_price_nonnegative"]);
        Assert.Equal("(price_minor_units IS NULL AND price_currency_code IS NULL) OR (price_minor_units IS NOT NULL AND price_currency_code IS NOT NULL)",
            checks["ck_availability_price_consistency"]);
    }

    [Fact]
    public void Schedule_and_ingredient_checks_preserve_ISO_weekdays_positions_and_time_order()
    {
        using var context = CreateContext();
        var model = Model(context);
        Assert.Equal("position >= 0", Assert.Single(model.FindEntityType(typeof(MedicationActiveIngredientEntity))!.GetCheckConstraints()).Sql);
        var checks = model.FindEntityType(typeof(OpeningIntervalEntity))!.GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);
        Assert.Equal(3, checks.Count);
        Assert.Equal("day_of_week BETWEEN 1 AND 7", checks["ck_opening_intervals_weekday"]);
        Assert.Equal("position >= 0", checks["ck_opening_intervals_position"]);
        Assert.Equal("opens_at < closes_at", checks["ck_opening_intervals_time_order"]);
    }

    [Fact]
    public void PostgreSQL_types_preserve_integer_prices_local_times_and_UTC_instants()
    {
        using var context = CreateContext();
        var model = Model(context);
        var availability = model.FindEntityType(typeof(AvailabilityEntity))!;
        Assert.Equal("bigint", availability.FindProperty("PriceMinorUnits")!.GetColumnType());
        Assert.Equal(typeof(long?), availability.FindProperty("PriceMinorUnits")!.ClrType);
        Assert.Equal("timestamp with time zone", availability.FindProperty("UpdatedAt")!.GetColumnType());
        Assert.Equal(typeof(DateTimeOffset), availability.FindProperty("UpdatedAt")!.ClrType);
        var interval = model.FindEntityType(typeof(OpeningIntervalEntity))!;
        foreach (var name in new[] { "OpensAt", "ClosesAt" })
        {
            Assert.Equal(typeof(TimeOnly), interval.FindProperty(name)!.ClrType);
            Assert.Equal("time without time zone", interval.FindProperty(name)!.GetColumnType());
        }
        Assert.Equal("boolean", model.FindEntityType(typeof(MedicationEntity))!.FindProperty("PrescriptionRequired")!.GetColumnType());
    }

    [Fact]
    public void All_table_column_and_constraint_names_use_snake_case_without_implicit_shadow_columns()
    {
        using var context = CreateContext();
        foreach (var entity in Model(context).GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            Assert.Matches("^[a-z][a-z0-9_]*$", entity.GetTableName()!);
            Assert.Matches("^[a-z][a-z0-9_]*$", entity.FindPrimaryKey()!.GetName()!);
            foreach (var property in entity.GetProperties()) Assert.Matches("^[a-z][a-z0-9_]*$", property.GetColumnName(table)!);
            foreach (var foreignKey in entity.GetForeignKeys()) Assert.Matches("^[a-z][a-z0-9_]*$", foreignKey.GetConstraintName()!);
            foreach (var index in entity.GetIndexes()) Assert.Matches("^[a-z][a-z0-9_]*$", index.GetDatabaseName()!);
            Assert.Empty(entity.GetSeedData());
        }
    }

    [Fact]
    public async Task API_and_scoped_context_initialization_do_not_open_a_database_connection()
    {
        var guard = new RejectDatabaseConnections();
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:PharmaFlow"] = "Host=127.0.0.1;Port=1;Database=pharmaflow_metadata_check;Timeout=1" }));
            builder.ConfigureServices(services => services.AddDbContext<PharmaFlowDbContext>(options => options.AddInterceptors(guard)));
        });
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        Assert.Equal(21, (await ApiFactory.GetArrayAsync(client, "/api/availability")).Count);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
        Assert.Same(first, firstScope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>());
        Assert.NotSame(first, secondScope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", first.Database.ProviderName);
        Assert.Equal(5, first.Model.GetEntityTypes().Count());
        Assert.Equal(ConnectionState.Closed, first.Database.GetDbConnection().State);
        Assert.Equal(0, guard.Attempts);
    }

    private sealed class RejectDatabaseConnections : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            Attempts++;
            throw new InvalidOperationException("Metadata/API verification must not connect to PostgreSQL.");
        }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Metadata/API verification must not connect to PostgreSQL.");
        }
    }
}
